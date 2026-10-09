namespace LogisticServer.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNo { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Status { get; set; } = "Created"; // Created, Searching, Offered, Assigned, PickedUp, Delivered, Cancelled, Failed, Unassigned
    public short Priority { get; set; } = 0;
    public string? VehicleType { get; set; } // Bike, Car, Van, Truck
    public string? PackageDescription { get; set; }
    public decimal? PackageWeight { get; set; }
    public DateTime? RequestedPickupAt { get; set; }

    public Guid? AssignedCourierId { get; set; }
    public Courier? AssignedCourier { get; set; }

    public string? CancelledReason { get; set; }
    public Guid? CancelledBy { get; set; }
    public User? CancelledByUser { get; set; }

    public int Version { get; set; } = 1; // Optimistic concurrency
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<OrderStop> Stops { get; set; } = new List<OrderStop>();
    public ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}

