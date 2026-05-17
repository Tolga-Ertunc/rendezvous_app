namespace Rendezvous.Domain.Staff;

public class StaffMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
}
