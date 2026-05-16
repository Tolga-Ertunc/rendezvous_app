using Microsoft.AspNetCore.Identity;

namespace Rendezvous.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public int PublicNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
