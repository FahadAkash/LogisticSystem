namespace LogisticServer.Configuration;

public class JwtConfig
{
    public string Issuer { get; set; } = "LogisticPlatform";
    public string Audience { get; set; } = "LogisticPlatform";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 7;
    public int WebSocketTicketLifetimeSeconds { get; set; } = 60;
    public string KeyId { get; set; } = "logistic-auth-key-1";
    public string? RsaPrivateKeyPem { get; set; }
    public string? RsaPublicKeyPem { get; set; }
    public string KeyDirectory { get; set; } = "keys";
}

