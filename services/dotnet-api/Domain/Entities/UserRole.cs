namespace LogisticServer.Domain.Entities;

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public short RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
