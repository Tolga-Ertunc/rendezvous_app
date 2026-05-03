using Microsoft.AspNetCore.Identity;

namespace Rendezvous.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public int PublicNumber { get; set; }
}
