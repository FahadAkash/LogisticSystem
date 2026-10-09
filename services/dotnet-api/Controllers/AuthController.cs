using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtKeyService _keyService;
    private readonly IWebSocketTicketService _ticketService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IJwtKeyService keyService,
        IWebSocketTicketService ticketService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _keyService = keyService;
        _ticketService = ticketService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new Customer or Courier account.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), null, response);
    }

    /// <summary>
    /// Authenticates a user with email and password credentials.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Rotates an existing refresh token and returns a new access token and refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Revokes the specified refresh token and terminates the session.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves the profile and roles of the currently authenticated user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Issues a short-lived, single-use ticket for authenticating WebSocket connections to go-gateway.
    /// </summary>
    [HttpPost("ws-ticket")]
    [Authorize]
    [ProducesResponseType(typeof(WebSocketTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebSocketTicketResponse>> GetWebSocketTicket(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(User.FindAll("role").Select(c => c.Value))
            .Distinct();

        var ticket = await _ticketService.CreateTicketAsync(userId, roles, cancellationToken);
        return Ok(ticket);
    }

    /// <summary>
    /// Serves the RFC 7517 JSON Web Key Set (JWKS) containing public keys for token verification by Go services.
    /// </summary>
    [HttpGet("jwks")]
    [HttpGet("/.well-known/jwks.json")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(JwksResponse), StatusCodes.Status200OK)]
    public ActionResult<JwksResponse> GetJwks()
    {
        var jwks = _keyService.GetJwks();
        return Ok(jwks);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing user identity in access token.");
        }

        return userId;
    }
}

