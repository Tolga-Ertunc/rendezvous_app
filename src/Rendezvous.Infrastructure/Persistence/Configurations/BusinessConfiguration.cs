using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.HasKey(business => business.Id);

        builder.Property(business => business.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(business => business.TimeZoneId)
            .HasMaxLength(100)
            .HasDefaultValue("Europe/Istanbul")
            .IsRequired();

        builder.Property(business => business.AddressLine)
            .HasMaxLength(240)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(business => business.District)
            .HasMaxLength(120)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(business => business.City)
            .HasMaxLength(120)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(business => business.Country)
            .HasMaxLength(120)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(business => business.Description)
            .HasMaxLength(1200)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(business => business.Type)
            .IsRequired();

        builder.Property(business => business.Status)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(business => business.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(business => business.Status);
    }
}
