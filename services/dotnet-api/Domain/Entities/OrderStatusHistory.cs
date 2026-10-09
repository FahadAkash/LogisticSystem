namespace LogisticServer.Domain.Entities;

public class OrderStatusHistory
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string ActorType { get; set; } = "System"; // Customer, Courier, Dispatcher, Admin, System
    public Guid? ActorId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
