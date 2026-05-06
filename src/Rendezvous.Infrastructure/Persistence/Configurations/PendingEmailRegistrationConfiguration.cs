using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class PendingEmailRegistrationConfiguration : IEntityTypeConfiguration<PendingEmailRegistration>
{
    public void Configure(EntityTypeBuilder<PendingEmailRegistration> builder)
    {
        builder.ToTable("PendingEmailRegistrations");

        builder.HasKey(registration => registration.Id);

        builder.Property(registration => registration.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(registration => registration.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(registration => registration.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(registration => registration.ConfirmationCodeHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(registration => registration.NormalizedEmail)
            .IsUnique();

        builder.HasIndex(registration => registration.CodeExpiresAtUtc);
    }
}
