using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Staff;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class StaffWorkingHourConfiguration : IEntityTypeConfiguration<StaffWorkingHour>
{
    public void Configure(EntityTypeBuilder<StaffWorkingHour> builder)
    {
        builder.HasKey(workingHour => workingHour.Id);

        builder.Property(workingHour => workingHour.DayOfWeek)
            .IsRequired();

        builder.Property(workingHour => workingHour.StartsAt)
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(workingHour => workingHour.EndsAt)
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(workingHour => workingHour.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workingHour => new { workingHour.StaffMemberId, workingHour.DayOfWeek })
            .IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_StaffWorkingHours_DayOfWeek",
                "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
            table.HasCheckConstraint(
                "CK_StaffWorkingHours_TimeRange",
                "\"StartsAt\" < \"EndsAt\"");
        });
    }
}
