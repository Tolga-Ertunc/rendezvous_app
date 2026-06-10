namespace Rendezvous.Domain.Appointments;

public class AppointmentStylePreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AppointmentId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid BusinessServiceId { get; set; }
    public Guid StaffMemberId { get; set; }
    public string OriginalStorageKey { get; set; } = string.Empty;
    public string OriginalContentType { get; set; } = string.Empty;
    public long OriginalFileSizeBytes { get; set; }
    public string GeneratedStorageKey { get; set; } = string.Empty;
    public string GeneratedContentType { get; set; } = string.Empty;
    public long GeneratedFileSizeBytes { get; set; }
    public bool IsPlaceholder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
