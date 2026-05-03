using FluentAssertions;
using Rendezvous.Domain.Appointments;

namespace Rendezvous.Tests.Domain.Appointments;

public class AppointmentApprovedCancellationTests
{
    [Fact]
    public void CancelApprovedAppointment_CancelsApprovedAppointmentTwoHoursBeforeStart()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddHours(2));

        var cancelled = appointment.CancelApprovedAppointment(nowUtc);

        cancelled.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelApprovedAppointment_CancelsApprovedAppointmentExactlyOneHourBeforeStart()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddHours(1));

        var cancelled = appointment.CancelApprovedAppointment(nowUtc);

        cancelled.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelApprovedAppointment_DoesNotCancelApprovedAppointmentFiftyNineMinutesBeforeStart()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddMinutes(59));

        var cancelled = appointment.CancelApprovedAppointment(nowUtc);

        cancelled.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.Approved);
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Rejected)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Expired)]
    public void CancelApprovedAppointment_DoesNotCancelNonApprovedAppointmentStatuses(AppointmentStatus status)
    {
        var appointment = CreateAppointment(status, DateTimeOffset.UtcNow.AddHours(2));

        var cancelled = appointment.CancelApprovedAppointment(DateTimeOffset.UtcNow);

        cancelled.Should().BeFalse();
        appointment.Status.Should().Be(status);
    }

    private static Appointment CreateAppointment(
        AppointmentStatus status,
        DateTimeOffset? startsAtUtc = null)
    {
        return new Appointment
        {
            BusinessId = Guid.NewGuid(),
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            StartsAtUtc = startsAtUtc ?? DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc = (startsAtUtc ?? DateTimeOffset.UtcNow.AddDays(1)).AddMinutes(30),
            Status = status,
            PriceAmount = 500,
            CurrencyCode = "TRY"
        };
    }
}
