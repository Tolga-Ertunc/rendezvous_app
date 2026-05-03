using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessMembershipConfiguration : IEntityTypeConfiguration<BusinessMembership>
{
    public void Configure(EntityTypeBuilder<BusinessMembership> builder)
    {
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Role)
            .IsRequired();

        builder.Property(membership => membership.Status)
            .IsRequired();

        builder.Property(membership => membership.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(membership => membership.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.BusinessId, membership.UserId })
            .IsUnique();

        builder.HasIndex(membership => new { membership.UserId, membership.Status });
    }
}
