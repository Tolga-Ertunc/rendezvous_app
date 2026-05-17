namespace Rendezvous.Domain.Businesses;

public class OwnerOnboardingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterUserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; } = BusinessType.Barber;
    public OwnerOnboardingRequestStatus Status { get; set; } = OwnerOnboardingRequestStatus.Pending;
    public string? AdminNote { get; set; }
    public Guid? CreatedBusinessId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
}
