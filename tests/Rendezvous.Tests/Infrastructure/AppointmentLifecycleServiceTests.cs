using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Infrastructure;

public class AppointmentLifecycleServiceTests
{
    [Fact]
    public async Task ProcessDueAppointmentsAsync_expires_pending_and_completes_approved_appointments()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AppointmentLifecycle_{Guid.NewGuid():N}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var pendingStartedAppointment = CreateAppointment(
            AppointmentStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(29));
        var futurePendingAppointment = CreateAppointment(
            AppointmentStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(10),
            DateTimeOffset.UtcNow.AddMinutes(40));
        var approvedDueAppointment = CreateAppointment(
            AppointmentStatus.Approved,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(-1));
        var approvedRecentAppointment = CreateAppointment(
            AppointmentStatus.Approved,
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(-5));

        dbContext.Appointments.AddRange(
            pendingStartedAppointment,
            futurePendingAppointment,
            approvedDueAppointment,
            approvedRecentAppointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ProcessDueAppointmentsAsync(CancellationToken.None);
        var secondResult = await service.ProcessDueAppointmentsAsync(CancellationToken.None);

        result.ExpiredCount.Should().Be(1);
        result.CompletedCount.Should().Be(1);
        secondResult.ExpiredCount.Should().Be(0);
        secondResult.CompletedCount.Should().Be(0);
        pendingStartedAppointment.Status.Should().Be(AppointmentStatus.Expired);
        futurePendingAppointment.Status.Should().Be(AppointmentStatus.Pending);
        approvedDueAppointment.Status.Should().Be(AppointmentStatus.Completed);
        approvedRecentAppointment.Status.Should().Be(AppointmentStatus.Approved);

        dbContext.Notifications
            .Where(notification => notification.Type == NotificationType.AppointmentExpired)
            .Should()
            .ContainSingle(notification => notification.UserId == pendingStartedAppointment.CustomerUserId);
        dbContext.Notifications
            .Should()
            .NotContain(notification => notification.UserId == approvedDueAppointment.CustomerUserId);
    }

    private static AppointmentLifecycleService CreateService(AppDbContext dbContext)
    {
        var notificationWriter = new NotificationWriter(dbContext);
        var notificationService = new AppointmentNotificationService(dbContext, notificationWriter);

        return new AppointmentLifecycleService(dbContext, notificationService);
    }

    private static Appointment CreateAppointment(
        AppointmentStatus status,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc)
    {
        return new Appointment
        {
            BusinessId = Guid.NewGuid(),
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            Status = status,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            PriceAmount = 100,
            CurrencyCode = "TRY"
        };
    }
}
