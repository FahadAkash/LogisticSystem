namespace LogisticServer.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid(); // becomes eventId
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string MessageKey { get; set; } = string.Empty; // Kafka partition key
    public string EventType { get; set; } = string.Empty;
    public int EventVersion { get; set; } = 1;
    public string Payload { get; set; } = string.Empty; // jsonb
    public string Headers { get; set; } = "{}"; // jsonb
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}

