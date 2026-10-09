using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LogisticServer.Application.DTOs.Couriers;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouriersController : ControllerBase
{
    private readonly ICourierService _courierService;
    private readonly ILogger<CouriersController> _logger;

    public CouriersController(ICourierService courierService, ILogger<CouriersController> logger)
    {
        _courierService = courierService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all registered couriers with status and vehicle filters (Dispatcher/Admin).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Dispatcher,Admin")]
    [ProducesResponseType(typeof(PagedResult<CourierDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<CourierDetailResponse>>> GetCouriers(
        [FromQuery] CourierFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _courierService.GetCouriersAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full courier profile and vehicle info by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CourierDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourierDetailResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _courierService.GetCourierByIdAsync(id, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves current authenticated courier's own profile and active assignment.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "Courier")]
    [ProducesResponseType(typeof(CourierDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourierDetailResponse>> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _courierService.GetCourierByUserIdAsync(userId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Updates courier availability status (Available, Offline, Busy). Syncs Redis GEO set and emits courier.status.changed.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Courier,Admin")]
    [ProducesResponseType(typeof(CourierDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourierDetailResponse>> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateCourierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = GetCurrentUserRoles();
        var correlationId = GetCorrelationId();

        var response = await _courierService.UpdateStatusAsync(id, userId, roles, request.Status, correlationId, cancellationToken);
        return Ok(response);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Missing or invalid user identity claim.");
        }

        return userId;
    }

    private List<string> GetCurrentUserRoles()
    {
        return User.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(User.FindAll("role").Select(c => c.Value))
            .Distinct()
            .ToList();
    }

    private string GetCorrelationId()
    {
        return HttpContext.Items["CorrelationId"]?.ToString() 
            ?? Guid.NewGuid().ToString("D");
    }
}
