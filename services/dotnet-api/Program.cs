using LogisticServer.Configuration;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Database & Redis settings from configuration and environment variables
var dbConfig = builder.Configuration.GetSection("Postgres").Get<DatabaseConfig>() ?? new DatabaseConfig();
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSTGRES_USER")))
    dbConfig.Username = Environment.GetEnvironmentVariable("POSTGRES_USER")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")))
    dbConfig.Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSTGRES_DB")))
    dbConfig.Database = Environment.GetEnvironmentVariable("POSTGRES_DB")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSTGRES_HOST")))
    dbConfig.Host = Environment.GetEnvironmentVariable("POSTGRES_HOST")!;

var redisConfig = builder.Configuration.GetSection("Redis").Get<RedisConfig>() ?? new RedisConfig();
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REDIS_PASSWORD")))
    redisConfig.Password = Environment.GetEnvironmentVariable("REDIS_PASSWORD")!;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REDIS_HOST")))
    redisConfig.Host = Environment.GetEnvironmentVariable("REDIS_HOST")!;

var kafkaConfig = builder.Configuration.GetSection("Kafka").Get<KafkaConfig>() ?? new KafkaConfig();
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")))
    kafkaConfig.BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")!;

builder.Services.AddSingleton(dbConfig);
builder.Services.AddSingleton(redisConfig);
builder.Services.AddSingleton(kafkaConfig);

// Register CoreDbContext with PostgreSQL & NetTopologySuite (PostGIS)
builder.Services.AddDbContext<CoreDbContext>(options =>
{
    options.UseNpgsql(dbConfig.BuildConnectionString(), npgsqlOptions =>
    {
        npgsqlOptions.UseNetTopologySuite();
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "core");
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
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
    await db.Database.MigrateAsync();
    Console.WriteLine("Database migrations applied successfully.");
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAngularDev");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");
app.MapControllers();

app.Run();
