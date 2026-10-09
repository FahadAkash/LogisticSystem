using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogisticServer.Tests;

public class InteropTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public InteropTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Jwks_Endpoint_ConformsToRfc7517_ForGoConsumer()
    {
        // Act: Fetch JWKS public keys
        var response = await _client.GetAsync("/.well-known/jwks.json");

        // Assert: Format matches Go parser expectations
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jwks = await response.Content.ReadFromJsonAsync<JwksResponse>();
        Assert.NotNull(jwks);
        Assert.NotEmpty(jwks.Keys);

        var key = jwks.Keys.First();
        Assert.Equal("RSA", key.Kty);
        Assert.Equal("RS256", key.Alg);
        Assert.Equal("sig", key.Use);
        Assert.False(string.IsNullOrWhiteSpace(key.Kid));
        Assert.False(string.IsNullOrWhiteSpace(key.N));
        Assert.False(string.IsNullOrWhiteSpace(key.E));
    }

    [Fact]
    public async Task JwtToken_ContainsRequiredClaims_ForGoGatewayVerification()
    {
        // Arrange: Register customer
        var email = $"interop_{Guid.NewGuid():N}@example.com";
        var regRequest = new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FullName = "Interop Tester",
            Role = "Customer"
        };

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // Act: Parse JWT using standard token handler
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(auth.AccessToken);

        // Assert: Claims expected by Go ValidateJWT
        Assert.NotNull(token);
        Assert.Equal("RS256", token.Header.Alg);
        Assert.False(string.IsNullOrEmpty(token.Header.Kid));

        var sub = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.NotNull(sub);
        Assert.True(Guid.TryParse(sub, out _), "Subject claim must be a valid UUID for Go uuid.Parse");

        var role = token.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        Assert.Equal("Customer", role);

        var exp = token.ValidTo;
        Assert.True(exp > DateTime.UtcNow, "Token must be valid in the future");
    }

    [Fact]
    public async Task WebSocketTicket_GeneratesExpectedContract_ForGoGateway()
    {
        // Arrange: Authenticate user
        var email = $"wsticket_{Guid.NewGuid():N}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FullName = "WS Ticket User",
            Role = "Customer"
        });
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // Act: Request WebSocket ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var ticketResp = await _client.PostAsync("/api/auth/ws-ticket", null);

        // Assert: Ticket response adheres to contract
        Assert.Equal(HttpStatusCode.OK, ticketResp.StatusCode);
        var ticket = await ticketResp.Content.ReadFromJsonAsync<WebSocketTicketResponse>();
        Assert.NotNull(ticket);
        Assert.False(string.IsNullOrWhiteSpace(ticket.Ticket));
        Assert.True(ticket.ExpiresIn > 0);
        Assert.True(ticket.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task OrderCreation_ProducesOutboxEvent_MatchingGoDomainModel()
    {
        // Arrange: Register customer
        var email = $"order_interop_{Guid.NewGuid():N}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FullName = "Order Interop User",
            Role = "Customer"
        });
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var orderRequest = new CreateOrderRequest
        {
            Priority = 1,
            VehicleType = "Car",
            PackageDescription = "Contract Verification Package",
            PackageWeight = 2.5m,
            RequestedPickupAt = null,
            Stops = new List<CreateOrderStopRequest>
            {
                new() { Sequence = 1, Type = "Pickup", Address = "100 Broadway, NY", Latitude = 40.7128, Longitude = -74.0060, ContactName = "Pickup Contact" },
                new() { Sequence = 2, Type = "Dropoff", Address = "200 Broadway, NY", Latitude = 40.7138, Longitude = -74.0050, ContactName = "Dropoff Contact" }
            }
        };

        var requestMsg = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(orderRequest)
        };
        requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        requestMsg.Headers.Add("Idempotency-Key", $"interop-idem-{Guid.NewGuid():N}");

        // Act: Create order
        var orderResp = await _client.SendAsync(requestMsg);
        Assert.Equal(HttpStatusCode.Created, orderResp.StatusCode);
        var createdOrder = await orderResp.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(createdOrder);

        // Assert: Query OutboxMessage from PostgreSQL and verify JSON schema
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var outbox = await db.OutboxMessages
            .Where(o => o.AggregateId == createdOrder.Id && o.EventType == "order.created")
            .FirstOrDefaultAsync();

        Assert.NotNull(outbox);
        Assert.Equal("order.events", outbox.Topic);

        // Parse JSON payload to ensure all fields expected by Go OrderCreatedEvent exist
        using var doc = JsonDocument.Parse(outbox.Payload);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("orderId", out var orderIdProp));
        Assert.Equal(createdOrder.Id, orderIdProp.GetGuid());

        Assert.True(root.TryGetProperty("pickupLatitude", out var pLat));
        Assert.Equal(40.7128, pLat.GetDouble(), precision: 4);

        Assert.True(root.TryGetProperty("pickupLongitude", out var pLon));
        Assert.Equal(-74.0060, pLon.GetDouble(), precision: 4);

        Assert.True(root.TryGetProperty("dropoffLatitude", out var dLat));
        Assert.Equal(40.7138, dLat.GetDouble(), precision: 4);

        Assert.True(root.TryGetProperty("vehicleType", out var vType));
        Assert.Equal("Car", vType.GetString());
    }
}
