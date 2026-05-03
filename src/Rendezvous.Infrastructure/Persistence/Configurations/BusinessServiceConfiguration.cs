using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessServiceConfiguration : IEntityTypeConfiguration<BusinessService>
{
    public void Configure(EntityTypeBuilder<BusinessService> builder)
    {
        builder.HasKey(service => service.Id);

        builder.Property(service => service.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(service => service.BasePriceAmount)
            .HasPrecision(18, 2);

        builder.Property(service => service.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(service => service.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(service => new { service.BusinessId, service.IsActive });
    }
}
