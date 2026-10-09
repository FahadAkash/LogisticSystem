using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace LogisticServer.Infrastructure.Filters;

public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly IIdempotencyRepository _repository;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(IIdempotencyRepository repository, ILogger<IdempotencyFilter> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var hasKey = httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var rawKey);
        var key = rawKey.ToString().Trim();

        var requiresKey = context.ActionDescriptor.EndpointMetadata
            .OfType<RequireIdempotencyKeyAttribute>()
            .Any();

        if (string.IsNullOrWhiteSpace(key))
        {
            if (requiresKey)
            {
                context.Result = new BadRequestObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Missing Idempotency-Key",
                    Detail = "The 'Idempotency-Key' request header is required for this operation.",
                    Instance = httpContext.Request.Path
                });
                return;
            }

            await next();
            return;
        }

        // 1. Resolve user ID for user-scoped idempotency
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        var userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

        // 2. Compute request payload hash
        var requestArg = context.ActionArguments.Values.FirstOrDefault();
        var requestJson = requestArg != null ? JsonSerializer.Serialize(requestArg) : string.Empty;
        var requestHash = ComputeSha256(requestJson);

        // 3. Lookup existing idempotency record
        var existing = await _repository.GetAsync(userId, key, httpContext.RequestAborted);
        if (existing != null)
        {
            if (existing.ExpiresAt > DateTime.UtcNow)
            {
                if (string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Idempotency HIT for user {UserId} with key '{Key}'. Returning saved response.", userId, key);
                    httpContext.Response.Headers["X-Idempotency-Cache"] = "HIT";

                    context.Result = new ContentResult
                    {
                        Content = existing.ResponseBody ?? string.Empty,
                        ContentType = "application/json",
                        StatusCode = existing.ResponseStatus ?? StatusCodes.Status200OK
                    };
                    return;
                }
                else
                {
                    _logger.LogWarning("Idempotency MISMATCH for user {UserId} with key '{Key}'. Different body provided.", userId, key);
                    context.Result = new ConflictObjectResult(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Idempotency Key Conflict",
                        Detail = "This idempotency key has already been used with a different request payload.",
                        Instance = httpContext.Request.Path
                    });
                    return;
                }
            }
        }

        // 4. Execute the endpoint action
        var executedContext = await next();

        // 5. Store successful response for future idempotent replays
        if (executedContext.Result is ObjectResult objectResult && (objectResult.StatusCode == null || (objectResult.StatusCode >= 200 && objectResult.StatusCode < 300)))
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
            var responseBody = JsonSerializer.Serialize(objectResult.Value);

            var record = new IdempotencyKey
            {
                UserId = userId,
                Key = key,
                RequestHash = requestHash,
                ResponseStatus = statusCode,
                ResponseBody = responseBody,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            try
            {
                await _repository.CreateOrUpdateAsync(record, CancellationToken.None);
                await _repository.SaveChangesAsync(CancellationToken.None);
                _logger.LogDebug("Stored idempotency record for key '{Key}'.", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist idempotency key '{Key}'.", key);
            }
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
