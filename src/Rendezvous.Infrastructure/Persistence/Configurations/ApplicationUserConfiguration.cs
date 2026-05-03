using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.PublicNumber)
            .IsRequired();

        builder.HasIndex(user => user.PublicNumber)
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_AspNetUsers_PublicNumber_8Digits",
                "\"PublicNumber\" >= 10000000 AND \"PublicNumber\" <= 99999999"));
    }
}
