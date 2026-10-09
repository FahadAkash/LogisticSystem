using NetTopologySuite.Geometries;

namespace LogisticServer.Domain.Entities;

public class OrderStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public short Sequence { get; set; }
    public string Type { get; set; } = "Pickup"; // Pickup, Dropoff
    public string Address { get; set; } = string.Empty;
    public Point Location { get; set; } = null!; // geography(Point, 4326)
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
