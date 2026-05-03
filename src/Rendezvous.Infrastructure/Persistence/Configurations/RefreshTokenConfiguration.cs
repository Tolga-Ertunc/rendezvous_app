using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(refreshToken => refreshToken.Id);

        builder.Property(refreshToken => refreshToken.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.CreatedAtUtc)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.ExpiresAtUtc)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(refreshToken => refreshToken.TokenHash)
            .IsUnique();

        builder.HasIndex(refreshToken => new { refreshToken.UserId, refreshToken.ExpiresAtUtc });
    }
}
