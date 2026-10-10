using System.Security.Claims;
using System.Text.Json.Serialization;
using LogisticServer.Application.Interfaces;
using LogisticServer.Configuration;
using LogisticServer.Infrastructure.Data;
using LogisticServer.Infrastructure.Filters;
using LogisticServer.Infrastructure.Middleware;
using LogisticServer.Infrastructure.Repositories;
using LogisticServer.Infrastructure.Security;
using LogisticServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

// Load .env file securely from workspace root if present locally
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging per Agent.md Rule 11.8 & 15.2
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "dotnet-api")
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// Configure Database, Redis, Kafka, and JWT settings
var dbConfig = builder.Configuration.GetSection("Postgres").Get<DatabaseConfig>() ?? new DatabaseConfig();
var host = Environment.GetEnvironmentVariable("DB_HOST") ?? Environment.GetEnvironmentVariable("POSTGRES_HOST");
if (!string.IsNullOrEmpty(host)) dbConfig.Host = host;

var portStr = Environment.GetEnvironmentVariable("DB_PORT") ?? Environment.GetEnvironmentVariable("POSTGRES_PORT");
if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out var p)) dbConfig.Port = p;

var user = Environment.GetEnvironmentVariable("DB_USER") ?? Environment.GetEnvironmentVariable("POSTGRES_USER");
if (!string.IsNullOrEmpty(user)) dbConfig.Username = user;

var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
if (!string.IsNullOrEmpty(pass)) dbConfig.Password = pass;

var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? Environment.GetEnvironmentVariable("POSTGRES_DB");
if (!string.IsNullOrEmpty(dbName)) dbConfig.Database = dbName;

var redisConfig = builder.Configuration.GetSection("Redis").Get<RedisConfig>() ?? new RedisConfig();
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REDIS_PASSWORD")))
    redisConfig.Password = Environment.GetEnvironmentVariable("REDIS_PASSWORD")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REDIS_HOST")))
    redisConfig.Host = Environment.GetEnvironmentVariable("REDIS_HOST")!;

var kafkaConfig = builder.Configuration.GetSection("Kafka").Get<KafkaConfig>() ?? new KafkaConfig();
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")))
    kafkaConfig.BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")!;

var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig();
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_ISSUER")))
    jwtConfig.Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_AUDIENCE")))
    jwtConfig.Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")!;
if (int.TryParse(Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_LIFETIME_MINUTES"), out var tokenMins))
    jwtConfig.AccessTokenLifetimeMinutes = tokenMins;
if (int.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_LIFETIME_DAYS"), out var refreshDays))
    jwtConfig.RefreshTokenLifetimeDays = refreshDays;
if (int.TryParse(Environment.GetEnvironmentVariable("JWT_WS_TICKET_LIFETIME_SECONDS"), out var ticketSecs))
    jwtConfig.WebSocketTicketLifetimeSeconds = ticketSecs;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_KEY_ID")))
    jwtConfig.KeyId = Environment.GetEnvironmentVariable("JWT_KEY_ID")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_RSA_PRIVATE_KEY_PEM")))
    jwtConfig.RsaPrivateKeyPem = Environment.GetEnvironmentVariable("JWT_RSA_PRIVATE_KEY_PEM")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_RSA_PUBLIC_KEY_PEM")))
    jwtConfig.RsaPublicKeyPem = Environment.GetEnvironmentVariable("JWT_RSA_PUBLIC_KEY_PEM")!;

builder.Services.AddSingleton(dbConfig);
builder.Services.AddSingleton(redisConfig);
builder.Services.AddSingleton(kafkaConfig);
builder.Services.AddSingleton(jwtConfig);

// Register CoreDbContext with PostgreSQL & NetTopologySuite (PostGIS)
builder.Services.AddDbContext<CoreDbContext>(options =>
{
    options.UseNpgsql(dbConfig.BuildConnectionString(), npgsqlOptions =>
    {
        npgsqlOptions.UseNetTopologySuite();
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "core");
    });
});

// Register Repositories, Security & Application services
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ICourierRepository, CourierRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

builder.Services.AddSingleton<IJwtKeyService, RsaKeyService>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourierService, CourierService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IdempotencyFilter>();

// Register Redis Connection Multiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(redisConfig.BuildConnectionString());
    }
    catch (Exception ex)
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not establish initial connection to Redis at {Host}:{Port}", redisConfig.Host, redisConfig.Port);
        return ConnectionMultiplexer.Connect(redisConfig.BuildConnectionString());
    }
});
builder.Services.AddScoped<IWebSocketTicketService, WebSocketTicketService>();

// Configure Asymmetric JWT Authentication per Agent.md Rule 11.7
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IJwtKeyService, JwtConfig>((options, keyService, config) =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyService.GetPublicKey(),
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = config.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

// Configure Role Authorization Policies per Agent.md Rule 11.7
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireDispatcher", policy => policy.RequireRole("Dispatcher", "Admin"));
    options.AddPolicy("RequireCourier", policy => policy.RequireRole("Courier"));
    options.AddPolicy("RequireCustomer", policy => policy.RequireRole("Customer"));
});

// Configure Global Exception Handling per dotnet-webapi skill
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Configure Controllers with string enum converters per dotnet-webapi skill
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrEmpty(origin)) return false;
                  if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                  {
                      return uri.Host == "localhost" ||
                             uri.Host == "127.0.0.1" ||
                             uri.Host.StartsWith("192.168.") ||
                             uri.Host.StartsWith("10.") ||
                             uri.Host.StartsWith("172.");
                  }
                  return false;
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Run migrations if --migrate flag is provided
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    await DatabaseInitializer.MigrateAndInitializeAllAsync(db, app.Logger);
    Console.WriteLine("All database migrations and schemas applied successfully.");
    return;
}

// Global exception handling & diagnostics middleware
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAngularDev");

// Prometheus HTTP metrics instrumentation per Agent.md Rule 11.9 & 15.1
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

// Health checks (both root and /api prefixes for direct and Nginx-routed traffic)
app.MapHealthChecks("/health");
app.MapHealthChecks("/api/health");
app.MapHealthChecks("/ready");
app.MapHealthChecks("/api/ready");

// Prometheus scraping endpoints (root and /api prefixes)
app.MapMetrics("/metrics");
app.MapMetrics("/api/metrics");

app.MapControllers();

app.Run();

static void LoadDotEnv()
{
    try
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var envPath = Path.Combine(current.FullName, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                    var idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        var key = trimmed[..idx].Trim();
                        var val = trimmed[(idx + 1)..].Trim();
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                }
                break;
            }
            current = current.Parent;
        }
    }
    catch
    {
        // Ignore errors in environments where .env file is not present or inaccessible
    }
}

public partial class Program { }
