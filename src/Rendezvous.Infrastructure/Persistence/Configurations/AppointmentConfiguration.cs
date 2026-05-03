using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(appointment => appointment.Id);

        builder.Property(appointment => appointment.Status)
            .IsRequired();

        builder.Property(appointment => appointment.PriceAmount)
            .HasPrecision(18, 2);

        builder.Property(appointment => appointment.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(appointment => appointment.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BusinessService>()
            .WithMany()
            .HasForeignKey(appointment => appointment.BusinessServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(appointment => appointment.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(appointment => appointment.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(appointment => new
        {
            appointment.StaffMemberId,
            appointment.StartsAtUtc,
            appointment.EndsAtUtc
        });
    }
}
