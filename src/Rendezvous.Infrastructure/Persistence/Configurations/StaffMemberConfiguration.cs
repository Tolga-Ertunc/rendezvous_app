using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.HasKey(staffMember => staffMember.Id);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(staffMember => staffMember.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(staffMember => staffMember.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(staffMember => new { staffMember.BusinessId, staffMember.IsActive });
        builder.HasIndex(staffMember => new { staffMember.BusinessId, staffMember.UserId })
            .IsUnique();
    }
}
