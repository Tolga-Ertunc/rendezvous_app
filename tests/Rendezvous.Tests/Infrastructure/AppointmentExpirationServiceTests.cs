using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Appointments;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Infrastructure;

public class AppointmentExpirationServiceTests
{
    [Fact]
    public async Task ExpirePendingAppointmentsAsync_marks_started_pending_appointments_as_expired()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AppointmentExpiration_{Guid.NewGuid():N}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var pendingStartedAppointment = new Appointment
        {
            BusinessId = Guid.NewGuid(),
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            Status = AppointmentStatus.Pending,
            StartsAtUtc = DateTime.UtcNow.AddMinutes(-1),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(29),
            PriceAmount = 100,
            CurrencyCode = "TRY"
        };
        var futurePendingAppointment = new Appointment
        {
            BusinessId = pendingStartedAppointment.BusinessId,
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            Status = AppointmentStatus.Pending,
            StartsAtUtc = DateTime.UtcNow.AddMinutes(10),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(40),
            PriceAmount = 50,
            CurrencyCode = "TRY"
        };
        var approvedStartedAppointment = new Appointment
        {
            BusinessId = pendingStartedAppointment.BusinessId,
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            Status = AppointmentStatus.Approved,
            StartsAtUtc = DateTime.UtcNow.AddMinutes(-1),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(29),
            PriceAmount = 75,
            CurrencyCode = "TRY"
        };

        dbContext.Appointments.AddRange(
            pendingStartedAppointment,
            futurePendingAppointment,
            approvedStartedAppointment);
        await dbContext.SaveChangesAsync();

        var service = new AppointmentExpirationService(dbContext);

        await service.ExpirePendingAppointmentsAsync(CancellationToken.None);

        pendingStartedAppointment.Status.Should().Be(AppointmentStatus.Expired);
        futurePendingAppointment.Status.Should().Be(AppointmentStatus.Pending);
        approvedStartedAppointment.Status.Should().Be(AppointmentStatus.Approved);
    }
}
