using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessInvitation> BusinessInvitations => Set<BusinessInvitation>();
    public DbSet<BusinessMembership> BusinessMemberships => Set<BusinessMembership>();
    public DbSet<BusinessWorkingHour> BusinessWorkingHours => Set<BusinessWorkingHour>();
    public DbSet<BusinessService> BusinessServices => Set<BusinessService>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<StaffWorkingHour> StaffWorkingHours => Set<StaffWorkingHour>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
