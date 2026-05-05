using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Title)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(notification => notification.LinkUrl)
            .HasMaxLength(300);

        builder.Property(notification => notification.Type)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(notification => new { notification.UserId, notification.ReadAtUtc });
        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAtUtc });
    }
}
