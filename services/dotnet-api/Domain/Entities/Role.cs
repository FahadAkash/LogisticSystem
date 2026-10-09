namespace LogisticServer.Domain.Entities;

public class Role
{
    public short Id { get; set; }
    public string Name { get; set; } = string.Empty; // Admin, Dispatcher, Courier, Customer

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

