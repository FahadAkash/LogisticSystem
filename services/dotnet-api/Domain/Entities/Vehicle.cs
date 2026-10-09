namespace LogisticServer.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlateNo { get; set; } = string.Empty;
    public string Type { get; set; } = "Bike"; // Bike, Car, Van, Truck
    public decimal? CapacityWeight { get; set; } // kg
    public decimal? CapacityVolume { get; set; } // m3
    public string Status { get; set; } = "Active"; // Active, Maintenance, Retired
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Courier> Couriers { get; set; } = new List<Courier>();
}

