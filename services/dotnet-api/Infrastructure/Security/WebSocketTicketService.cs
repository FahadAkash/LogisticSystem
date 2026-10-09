using System.Security.Cryptography;
using System.Text.Json;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.Interfaces;
using LogisticServer.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace LogisticServer.Infrastructure.Security;

public class WebSocketTicketService : IWebSocketTicketService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly JwtConfig _config;
    private readonly ILogger<WebSocketTicketService> _logger;

    public WebSocketTicketService(
        JwtConfig config,
        ILogger<WebSocketTicketService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _config = config;
        _logger = logger;
        _redis = redis;
    }

    public async Task<WebSocketTicketResponse> CreateTicketAsync(
        Guid userId, 
        IEnumerable<string> roles, 
        CancellationToken cancellationToken = default)
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var ticket = Base64UrlEncoder.Encode(randomBytes);
        var ttlSeconds = _config.WebSocketTicketLifetimeSeconds;
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);

        var payload = new
        {
            userId = userId,
            roles = roles.ToArray(),
            issuedAt = DateTimeOffset.UtcNow,
            expiresAt = expiresAt
        };

        var json = JsonSerializer.Serialize(payload);

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"ws:ticket:{ticket}";
                await db.StringSetAsync(key, json, TimeSpan.FromSeconds(ttlSeconds));
                _logger.LogInformation("WebSocket ticket stored in Redis with key {Key} (TTL: {TTL}s)", key, ttlSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store WebSocket ticket in Redis.");
                throw;
            }
        }
        else
        {
            _logger.LogWarning("Redis is not connected. Ticket generated without Redis persistence.");
        }

        return new WebSocketTicketResponse(
            Ticket: ticket,
            ExpiresIn: ttlSeconds,
            ExpiresAt: expiresAt
        );
    }
}

