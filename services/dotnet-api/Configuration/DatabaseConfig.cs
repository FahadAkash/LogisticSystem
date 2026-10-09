namespace LogisticServer.Configuration;

public class DatabaseConfig
{
    public string Host { get; set; } = "172.19.0.6";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "appdb";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "postgres_secure_pass";
    public string SearchPath { get; set; } = "core";

    public string BuildConnectionString()
    {
        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SearchPath={SearchPath};";
    }
}

public class RedisConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = "redis_secure_pass";

    public string BuildConnectionString()
    {
        var auth = string.IsNullOrEmpty(Password) ? "" : $",password={Password}";
        return $"{Host}:{Port}{auth},abortConnect=false";
    }
}

public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "127.0.0.1:9094";
}

