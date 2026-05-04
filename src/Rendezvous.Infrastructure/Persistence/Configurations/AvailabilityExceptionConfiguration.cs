using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence.Configurations;

public class AvailabilityExceptionConfiguration : IEntityTypeConfiguration<AvailabilityException>
{
    public void Configure(EntityTypeBuilder<AvailabilityException> builder)
    {
        builder.HasKey(exception => exception.Id);

        builder.Property(exception => exception.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(exception => exception.Note)
            .HasMaxLength(500);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(exception => exception.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(exception => exception.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(exception => exception.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(exception => new
        {
            exception.BusinessId,
            exception.Date,
            exception.StaffMemberId
        });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_AvailabilityExceptions_TypeScope",
                """
                (("Type" IN (1, 2) AND "StaffMemberId" IS NULL)
                OR ("Type" = 3 AND "StaffMemberId" IS NOT NULL))
                """);
            table.HasCheckConstraint(
                "CK_AvailabilityExceptions_TimeRange",
                """
                (("IsFullDay" = TRUE AND "StartsAt" IS NULL AND "EndsAt" IS NULL)
                OR ("IsFullDay" = FALSE AND "StartsAt" IS NOT NULL AND "EndsAt" IS NOT NULL AND "StartsAt" < "EndsAt"))
                """);
        });
    }
}
