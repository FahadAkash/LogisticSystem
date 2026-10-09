namespace LogisticServer.Domain.Entities;

public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid CourierId { get; set; }
    public Courier Courier { get; set; } = null!;

    public string Status { get; set; } = "Assigned"; // Assigned, PickedUp, Completed, Cancelled, Reassigned
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
