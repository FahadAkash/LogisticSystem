namespace LogisticServer.Configuration;

public class DatabaseConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "appdb";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = string.Empty;
    public string SearchPath { get; set; } = "core,public";
    public int MaxPoolSize { get; set; } = 80;
    public int MinPoolSize { get; set; } = 10;
    public int ConnectionTimeoutSeconds { get; set; } = 15;

    public string BuildConnectionString()
    {
        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SearchPath={SearchPath};Pooling=true;Maximum Pool Size={MaxPoolSize};Minimum Pool Size={MinPoolSize};Timeout={ConnectionTimeoutSeconds};Connection Idle Lifetime=30;Connection Pruning Interval=10;";
    }
}

public class RedisConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;

    public string BuildConnectionString()
    {
        var auth = string.IsNullOrEmpty(Password) ? "" : $",password={Password}";
        return $"{Host}:{Port}{auth},abortConnect=false";
    }
}

public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "localhost:9092";
}
