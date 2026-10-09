using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ICourierService _courierService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ICourierService courierService, ILogger<AdminController> logger)
    {
        _courierService = courierService;
        _logger = logger;
    }

    /// <summary>
    /// Approves a pending courier registration, transitions status to Offline, and emits courier.registered event.
    /// </summary>
    [HttpPost("couriers/{id:guid}/approve")]
    [ProducesResponseType(typeof(CourierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourierResponse>> ApproveCourier(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() 
            ?? Guid.NewGuid().ToString("D");

        var response = await _courierService.ApproveCourierAsync(id, correlationId, cancellationToken);
        return Ok(response);
    }
}

