using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessServiceCategoryConfiguration : IEntityTypeConfiguration<BusinessServiceCategory>
{
    public void Configure(EntityTypeBuilder<BusinessServiceCategory> builder)
    {
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(category => category.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => new { category.BusinessId, category.Name })
            .IsUnique();

        builder.HasIndex(category => new { category.BusinessId, category.SortOrder });
    }
}
