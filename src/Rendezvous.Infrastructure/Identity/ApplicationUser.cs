using Microsoft.AspNetCore.Identity;

namespace Rendezvous.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public int PublicNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Guid? ProfilePhotoId { get; set; }
    public string? ProfilePhotoStorageKey { get; set; }
    public string? ProfilePhotoContentType { get; set; }
    public long? ProfilePhotoFileSizeBytes { get; set; }
    public DateTimeOffset? ProfilePhotoUpdatedAtUtc { get; set; }
}
