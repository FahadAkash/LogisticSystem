namespace LogisticServer.Domain.Entities;

public class IdempotencyKey
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; } // jsonb
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

