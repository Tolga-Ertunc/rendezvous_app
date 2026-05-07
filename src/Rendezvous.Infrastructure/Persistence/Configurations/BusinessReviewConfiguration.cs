using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessReviewConfiguration : IEntityTypeConfiguration<BusinessReview>
{
    public void Configure(EntityTypeBuilder<BusinessReview> builder)
    {
        builder.HasKey(review => review.Id);

        builder.Property(review => review.CustomerName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(review => review.CustomerInitial)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(review => review.Rating)
            .HasPrecision(3, 2);

        builder.Property(review => review.Comment)
            .HasMaxLength(1200)
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(review => review.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(review => new { review.BusinessId, review.IsPublic, review.CreatedAtUtc });
    }
}
