namespace LogisticServer.Domain.Entities;

public class Courier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public string LicenseNo { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingApproval"; // PendingApproval, Offline, Available, Busy, Suspended
    public decimal Rating { get; set; } = 5.00m;
    public DateTime? ApprovedAt { get; set; }
    public int Version { get; set; } = 1; // Optimistic concurrency
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ICollection<Order> AssignedOrders { get; set; } = new List<Order>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
