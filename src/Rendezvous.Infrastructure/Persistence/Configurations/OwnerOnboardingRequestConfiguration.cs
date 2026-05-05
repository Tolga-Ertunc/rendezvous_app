using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class OwnerOnboardingRequestConfiguration : IEntityTypeConfiguration<OwnerOnboardingRequest>
{
    public void Configure(EntityTypeBuilder<OwnerOnboardingRequest> builder)
    {
        builder.HasKey(request => request.Id);

        builder.Property(request => request.BusinessName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.OwnerStaffDisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.AdminNote)
            .HasMaxLength(500);

        builder.Property(request => request.BusinessType)
            .IsRequired();

        builder.Property(request => request.Status)
            .IsRequired();

        builder.Property(request => request.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(request => request.CreatedBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => new { request.RequesterUserId, request.Status });
        builder.HasIndex(request => request.Status);
    }
}
