namespace Rendezvous.Domain.Businesses;

public class BusinessMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid UserId { get; set; }
    public BusinessMembershipRole Role { get; set; } = BusinessMembershipRole.Employee;
    public BusinessMembershipStatus Status { get; set; } = BusinessMembershipStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
