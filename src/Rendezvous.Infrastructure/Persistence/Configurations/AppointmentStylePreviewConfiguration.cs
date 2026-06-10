using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class AppointmentStylePreviewConfiguration : IEntityTypeConfiguration<AppointmentStylePreview>
{
    public void Configure(EntityTypeBuilder<AppointmentStylePreview> builder)
    {
        builder.HasKey(preview => preview.Id);

        builder.Property(preview => preview.OriginalStorageKey)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(preview => preview.OriginalContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(preview => preview.GeneratedStorageKey)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(preview => preview.GeneratedContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(preview => preview.AppointmentId)
            .IsConcurrencyToken();

        builder.HasIndex(preview => preview.AppointmentId)
            .IsUnique();

        builder.HasIndex(preview => preview.CustomerUserId);
        builder.HasIndex(preview => preview.StaffMemberId);

        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(preview => preview.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(preview => preview.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(preview => preview.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BusinessService>()
            .WithMany()
            .HasForeignKey(preview => preview.BusinessServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(preview => preview.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
