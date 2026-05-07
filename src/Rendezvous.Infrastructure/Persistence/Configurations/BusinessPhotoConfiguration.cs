using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessPhotoConfiguration : IEntityTypeConfiguration<BusinessPhoto>
{
    public void Configure(EntityTypeBuilder<BusinessPhoto> builder)
    {
        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.ImageUrl)
            .HasMaxLength(600)
            .IsRequired();

        builder.Property(photo => photo.StorageKey)
            .HasMaxLength(600)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(photo => photo.ContentType)
            .HasMaxLength(80)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(photo => photo.AltText)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(photo => photo.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(photo => new { photo.BusinessId, photo.SortOrder });
    }
}
