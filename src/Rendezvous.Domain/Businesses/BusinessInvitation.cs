namespace Rendezvous.Domain.Businesses;

public class BusinessInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string StaffDisplayName { get; set; } = string.Empty;
    public BusinessMembershipRole Role { get; set; } = BusinessMembershipRole.Employee;
    public BusinessInvitationStatus Status { get; set; } = BusinessInvitationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? AcceptedAtUtc { get; set; }
}
