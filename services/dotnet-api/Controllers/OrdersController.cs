using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Application.Interfaces;
using LogisticServer.Infrastructure.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new delivery order with stops. Emits order.created outbox event.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer,Dispatcher,Admin")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [RequireIdempotencyKey]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var correlationId = GetCorrelationId();
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        var response = await _orderService.CreateOrderAsync(
            userId, 
            request, 
            idempotencyKey, 
            correlationId, 
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>
    /// Retrieves a paginated list of orders scoped by caller role and filters.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> GetOrders(
        [FromQuery] OrderFilterQuery query,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = GetCurrentUserRoles();

        var result = await _orderService.GetOrdersAsync(userId, roles, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves complete order details including stops, history, and assignments.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = GetCurrentUserRoles();

        var response = await _orderService.GetOrderByIdAsync(id, userId, roles, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Cancels an order before pickup (or by admin override). Emits order.cancelled event.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Customer,Dispatcher,Admin")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> CancelOrder(
        [FromRoute] Guid id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = GetCurrentUserRoles();
        var correlationId = GetCorrelationId();

        var response = await _orderService.CancelOrderAsync(id, userId, roles, request, correlationId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Transitions order status from Assigned to PickedUp. Emits order.status.changed event.
    /// </summary>
    [HttpPost("{id:guid}/pickup")]
    [Authorize(Roles = "Courier")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> PickupOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var correlationId = GetCorrelationId();

        var response = await _orderService.PickupOrderAsync(id, userId, correlationId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Transitions order status from PickedUp to Delivered and marks assignment complete. Emits order.status.changed event.
    /// </summary>
    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = "Courier")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> DeliverOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var correlationId = GetCorrelationId();

        var response = await _orderService.DeliverOrderAsync(id, userId, correlationId, cancellationToken);
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
