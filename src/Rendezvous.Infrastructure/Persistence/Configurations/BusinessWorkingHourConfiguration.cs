using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class BusinessWorkingHourConfiguration : IEntityTypeConfiguration<BusinessWorkingHour>
{
    public void Configure(EntityTypeBuilder<BusinessWorkingHour> builder)
    {
        builder.HasKey(workingHour => workingHour.Id);

        builder.Property(workingHour => workingHour.DayOfWeek)
            .IsRequired();

        builder.Property(workingHour => workingHour.OpensAt)
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(workingHour => workingHour.ClosesAt)
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(workingHour => workingHour.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workingHour => new { workingHour.BusinessId, workingHour.DayOfWeek })
            .IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_BusinessWorkingHours_DayOfWeek",
                "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
            table.HasCheckConstraint(
                "CK_BusinessWorkingHours_TimeRange",
                "\"OpensAt\" < \"ClosesAt\"");
        });
    }
}
