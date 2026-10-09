using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogisticServer.Tests;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetJwks_ReturnsValidJsonWebKeySet()
    {
        // Act
        var response = await _client.GetAsync("/.well-known/jwks.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JwksResponse>();
        Assert.NotNull(content);
        Assert.NotEmpty(content.Keys);

        var key = content.Keys.First();
        Assert.Equal("RSA", key.Kty);
        Assert.Equal("RS256", key.Alg);
        Assert.Equal("sig", key.Use);
        Assert.False(string.IsNullOrEmpty(key.Kid));
        Assert.False(string.IsNullOrEmpty(key.N));
        Assert.False(string.IsNullOrEmpty(key.E));
    }

    [Fact]
    public async Task Register_Customer_Returns201CreatedAndValidTokens()
    {
        // Arrange
        var uniqueEmail = $"cust_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest
        {
            Email = uniqueEmail,
            Password = "SecurePassword123!",
            FullName = "John Customer",
            Role = "Customer"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrEmpty(authResponse.AccessToken));
        Assert.False(string.IsNullOrEmpty(authResponse.RefreshToken));
        Assert.Equal("Bearer", authResponse.TokenType);
        Assert.Equal(uniqueEmail, authResponse.User.Email);
        Assert.Contains("Customer", authResponse.User.Roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        // Arrange
        var uniqueEmail = $"dup_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest
        {
            Email = uniqueEmail,
            Password = "SecurePassword123!",
            FullName = "First User",
            Role = "Customer"
        };

        var firstResp = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, firstResp.StatusCode);

        // Act
        var dupResp = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);
    }

    [Fact]
    public async Task Register_Courier_SetsStatusToPendingApprovalAndCreatesVehicle()
    {
        // Arrange
        var uniqueEmail = $"courier_{Guid.NewGuid():N}@example.com";
        var uniquePlate = $"P-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var request = new RegisterRequest
        {
            Email = uniqueEmail,
            Password = "SecurePassword123!",
            FullName = "Fast Courier",
            Role = "Courier",
            VehicleType = "Car",
            PlateNumber = uniquePlate
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Contains("Courier", authResponse.User.Roles);

        // Verify Courier status in database is PendingApproval per Agent.md 10.2
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var courier = await db.Couriers.Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.UserId == authResponse.User.Id);

        Assert.NotNull(courier);
        Assert.Equal("PendingApproval", courier.Status);
        Assert.NotNull(courier.Vehicle);
        Assert.Equal(uniquePlate, courier.Vehicle.PlateNo);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndProfile()
    {
        // Arrange
        var email = $"login_{Guid.NewGuid():N}@example.com";
        var password = "LoginPass123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            FullName = "Login User",
            Role = "Customer"
        });

        // Act
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var authResponse = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrEmpty(authResponse.AccessToken));
        Assert.Equal(email, authResponse.User.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401Unauthorized()
    {
        // Arrange
        var email = $"fail_{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "CorrectPassword123!",
            FullName = "User",
            Role = "Customer"
        });

        // Act
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "WrongPassword!"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }

    [Fact]
    public async Task Me_WithBearerToken_ReturnsProfile()
    {
        // Arrange
        var email = $"me_{Guid.NewGuid():N}@example.com";
        var regResp = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "MePassword123!",
            FullName = "Me User",
            Role = "Customer"
        });
        var auth = await regResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // Act
        var requestMsg = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResp = await _client.SendAsync(requestMsg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var user = await meResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Contains("Customer", user.Roles);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RotatesTokensAndDetectsReuse()
    {
        // Arrange
        var email = $"ref_{Guid.NewGuid():N}@example.com";
        var regResp = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "RefPassword123!",
            FullName = "Ref User",
            Role = "Customer"
        });
        var initialAuth = await regResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(initialAuth);

        // Act 1: Valid rotation
        var refreshResp1 = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = initialAuth.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResp1.StatusCode);
        var rotatedAuth = await refreshResp1.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(rotatedAuth);
        Assert.NotEqual(initialAuth.RefreshToken, rotatedAuth.RefreshToken);

        // Act 2: Replay attack (reusing old revoked refresh token)
        var replayResp = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = initialAuth.RefreshToken
        });

        // Assert: Revoked token rejected
        Assert.Equal(HttpStatusCode.Unauthorized, replayResp.StatusCode);
    }

    [Fact]
    public async Task AdminApproveCourier_TransitionsToOfflineAndWritesOutboxEvent()
    {
        // Arrange 1: Register new courier
        var courierEmail = $"approve_courier_{Guid.NewGuid():N}@example.com";
        var courierReg = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = courierEmail,
            Password = "CourierPass123!",
            FullName = "Driver Alex",
            Role = "Courier",
            VehicleType = "Van",
            PlateNumber = $"VAN-{Guid.NewGuid():N}"[..10].ToUpperInvariant()
        });
        var courierAuth = await courierReg.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(courierAuth);

        // Arrange 2: Login as default admin
        var adminLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@logistic.local",
            Password = "Admin1234!"
        });
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(adminAuth);

        // Act: Admin approves courier
        var approveMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/couriers/{courierAuth.User.Id}/approve");
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        approveMsg.Headers.Add("X-Correlation-Id", "test-corr-123");
        var approveResp = await _client.SendAsync(approveMsg);

        // Assert: Response is 200 OK with Offline status per Agent.md 10.2
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var courierResp = await approveResp.Content.ReadFromJsonAsync<CourierResponse>();
        Assert.NotNull(courierResp);
        Assert.Equal("Offline", courierResp.Status);

        // Verify outbox message in database in the SAME transaction per Agent.md Section 9 & 10.2
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var outboxMessage = await db.OutboxMessages
            .FirstOrDefaultAsync(o => o.AggregateId == courierResp.Id && o.EventType == "courier.registered");

        Assert.NotNull(outboxMessage);
        Assert.Equal("courier.events", outboxMessage.Topic);
        Assert.Null(outboxMessage.PublishedAt); // unpublished waiting for worker
        Assert.Contains("Offline", outboxMessage.Payload);
        Assert.Contains("test-corr-123", outboxMessage.Headers);
    }

    [Fact]
    public async Task AdminApproveCourier_NonAdminUser_Returns403Forbidden()
    {
        // Arrange: Register customer
        var customerEmail = $"cust_forbidden_{Guid.NewGuid():N}@example.com";
        var custReg = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = customerEmail,
            Password = "Password123!",
            FullName = "Regular Customer",
            Role = "Customer"
        });
        var custAuth = await custReg.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(custAuth);

        // Act: Customer attempts to call admin endpoint
        var approveMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/couriers/{Guid.NewGuid()}/approve");
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", custAuth.AccessToken);
        var response = await _client.SendAsync(approveMsg);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

