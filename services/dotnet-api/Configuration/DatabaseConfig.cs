namespace LogisticServer.Configuration;

public class DatabaseConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "appdb";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = string.Empty;
    public string SearchPath { get; set; } = "core,public";

    public string BuildConnectionString()
    {
        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SearchPath={SearchPath};";
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
