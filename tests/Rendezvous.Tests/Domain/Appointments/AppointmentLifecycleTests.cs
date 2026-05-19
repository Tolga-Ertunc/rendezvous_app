using FluentAssertions;
using Rendezvous.Domain.Appointments;

namespace Rendezvous.Tests.Domain.Appointments;

public class AppointmentLifecycleTests
{
    [Fact]
    public void ExpirePending_expires_only_started_pending_appointments()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Pending, nowUtc.AddMinutes(-1));

        var expired = appointment.ExpirePending(nowUtc);

        expired.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Expired);
    }

    [Fact]
    public void ExpirePending_does_not_expire_future_pending_appointment()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Pending, nowUtc.AddMinutes(1));

        var expired = appointment.ExpirePending(nowUtc);

        expired.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact]
    public void CompleteApprovedAppointment_automatically_completes_after_end_plus_delay()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(
            AppointmentStatus.Approved,
            nowUtc.AddHours(-2),
            nowUtc.Subtract(Appointment.AutomaticCompletionDelay).AddSeconds(-1));

        var completed = appointment.CompleteApprovedAppointment(nowUtc, automatic: true);

        completed.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public void CompleteApprovedAppointment_does_not_automatically_complete_before_delay()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(
            AppointmentStatus.Approved,
            nowUtc.AddHours(-1),
            nowUtc.Subtract(Appointment.AutomaticCompletionDelay).AddSeconds(1));

        var completed = appointment.CompleteApprovedAppointment(nowUtc, automatic: true);

        completed.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.Approved);
    }

    [Fact]
    public void CompleteApprovedAppointment_manually_completes_after_start()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddMinutes(-1));

        var completed = appointment.CompleteApprovedAppointment(nowUtc, automatic: false);

        completed.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public void CompleteApprovedAppointment_does_not_manually_complete_before_start()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddMinutes(1));

        var completed = appointment.CompleteApprovedAppointment(nowUtc, automatic: false);

        completed.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.Approved);
    }

    [Fact]
    public void MarkNoShow_marks_approved_appointment_after_start()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.Approved, nowUtc.AddMinutes(-1));

        var marked = appointment.MarkNoShow(nowUtc);

        marked.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.NoShow);
    }

    [Fact]
    public void MarkNoShow_marks_completed_appointment_inside_correction_window()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(
            AppointmentStatus.Completed,
            nowUtc.AddHours(-2),
            nowUtc.Subtract(Appointment.NoShowCorrectionWindow).AddSeconds(1));

        var marked = appointment.MarkNoShow(nowUtc);

        marked.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.NoShow);
    }

    [Fact]
    public void MarkNoShow_does_not_mark_completed_appointment_after_correction_window()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(
            AppointmentStatus.Completed,
            nowUtc.AddDays(-2),
            nowUtc.Subtract(Appointment.NoShowCorrectionWindow).AddSeconds(-1));

        var marked = appointment.MarkNoShow(nowUtc);

        marked.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public void CompleteApprovedAppointment_does_not_complete_no_show_appointment()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointment = CreateAppointment(AppointmentStatus.NoShow, nowUtc.AddHours(-1));

        var completed = appointment.CompleteApprovedAppointment(nowUtc, automatic: false);

        completed.Should().BeFalse();
        appointment.Status.Should().Be(AppointmentStatus.NoShow);
    }

    private static Appointment CreateAppointment(
        AppointmentStatus status,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc = null)
    {
        return new Appointment
        {
            BusinessId = Guid.NewGuid(),
            BusinessServiceId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc ?? startsAtUtc.AddMinutes(30),
            Status = status,
            PriceAmount = 500,
            CurrencyCode = "TRY"
        };
    }
}
