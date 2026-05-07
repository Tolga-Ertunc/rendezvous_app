namespace Rendezvous.Domain.Businesses;

public class Business
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; } = BusinessType.Barber;
    public BusinessStatus Status { get; set; } = BusinessStatus.PendingApproval;
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
    public string AddressLine { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool SupportsInstantConfirmation { get; set; } = true;
    public bool SupportsPayByApp { get; set; }
    public bool IsPetFriendly { get; set; }
    public bool IsKidFriendly { get; set; } = true;
    public bool IsNearPublicTransport { get; set; } = true;
    public bool UsesOrganicProducts { get; set; }
    public bool UsesVeganProducts { get; set; }
    public bool IsEnvironmentallyFriendly { get; set; }
}
