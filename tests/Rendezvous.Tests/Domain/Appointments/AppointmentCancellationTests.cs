using FluentAssertions;
using Rendezvous.Domain.Appointments;

namespace Rendezvous.Tests.Domain.Appointments;

public class AppointmentCancellationTests
{
    [Fact]
    public void CancelByCustomer_CancelsPendingAppointment()
    {
        var appointment = CreateAppointment(AppointmentStatus.Pending);

        var cancelled = appointment.CancelByCustomer(DateTimeOffset.UtcNow);

        cancelled.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelByCustomer_CancelsApprovedAppointmentTwoHoursBeforeStart()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddHours(2));

        var cancelled = appointment.CancelByCustomer(nowUtc);

        cancelled.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelByCustomer_CancelsApprovedAppointmentExactlyOneHourBeforeStart()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddHours(1));

        var cancelled = appointment.CancelByCustomer(nowUtc);

        cancelled.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelByCustomer_DoesNotCancelApprovedAppointmentFiftyNineMinutesBeforeStart()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddMinutes(59));

        var cancelled = appointment.CancelByCustomer(nowUtc);

        cancelled.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.Approved);
    }

    [Theory]
    [InlineData(AppointmentStatus.Rejected)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Expired)]
    public void CancelByCustomer_DoesNotCancelClosedAppointmentStatuses(AppointmentStatus status)
    {
        var appointment = CreateAppointment(status);

        var cancelled = appointment.CancelByCustomer(DateTimeOffset.UtcNow);

        cancelled.Should().BeFalse();
        appointment.Status.Should().Be(status);
    }

    private static Appointment CreateAppointment(
        AppointmentStatus status,
        DateTimeOffset? startsAtUtc = null)
    {
        var startUtc = startsAtUtc ?? DateTimeOffset.UtcNow.AddDays(1);

        return new Appointment
        {
            BusinessId = Guid.NewGuid(),
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            StartsAtUtc = startUtc,
            EndsAtUtc = startUtc.AddMinutes(30),
            Status = status,
            PriceAmount = 500,
            CurrencyCode = "TRY"
        };
    }
}
