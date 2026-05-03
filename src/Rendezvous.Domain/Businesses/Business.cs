namespace Rendezvous.Domain.Businesses;

public class Business
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; } = BusinessType.Barber;
    public BusinessStatus Status { get; set; } = BusinessStatus.PendingApproval;
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
}
