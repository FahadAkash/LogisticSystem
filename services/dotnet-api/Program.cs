using LogisticServer.Configuration;

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

builder.Services.AddSingleton(dbConfig);
builder.Services.AddSingleton(redisConfig);

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
