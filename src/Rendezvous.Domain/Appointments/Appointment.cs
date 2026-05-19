namespace Rendezvous.Domain.Appointments;

public class Appointment
{
    private static readonly TimeSpan ApprovedCancellationCutoff = TimeSpan.FromHours(1);

    public static TimeSpan AutomaticCompletionDelay { get; } = TimeSpan.FromMinutes(30);
    public static TimeSpan NoShowCorrectionWindow { get; } = TimeSpan.FromHours(24);

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid BusinessServiceId { get; set; }
    public Guid StaffMemberId { get; set; }
    public Guid CustomerUserId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public decimal PriceAmount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";

    public bool ExpirePending(DateTimeOffset nowUtc)
    {
        if (Status != AppointmentStatus.Pending || StartsAtUtc > nowUtc)
        {
            return false;
        }

        Status = AppointmentStatus.Expired;
        return true;
    }

    public bool CanApprovedAppointmentBeCompleted(DateTimeOffset nowUtc, bool automatic)
    {
        if (Status != AppointmentStatus.Approved)
        {
            return false;
        }

        return automatic
            ? EndsAtUtc + AutomaticCompletionDelay <= nowUtc
            : StartsAtUtc <= nowUtc;
    }

    public bool CompleteApprovedAppointment(DateTimeOffset nowUtc, bool automatic)
    {
        if (!CanApprovedAppointmentBeCompleted(nowUtc, automatic))
        {
            return false;
        }

        Status = AppointmentStatus.Completed;
        return true;
    }

    public bool CanBeMarkedNoShow(DateTimeOffset nowUtc)
    {
        return Status switch
        {
            AppointmentStatus.Approved => StartsAtUtc <= nowUtc,
            AppointmentStatus.Completed => nowUtc <= EndsAtUtc + NoShowCorrectionWindow,
            _ => false
        };
    }

    public bool MarkNoShow(DateTimeOffset nowUtc)
    {
        if (!CanBeMarkedNoShow(nowUtc))
        {
            return false;
        }

        Status = AppointmentStatus.NoShow;
        return true;
    }

    public bool CanBeCancelledByCustomer(DateTimeOffset nowUtc)
    {
        if (Status == AppointmentStatus.Pending)
        {
            return true;
        }

        if (Status != AppointmentStatus.Approved)
        {
            return false;
        }

        return StartsAtUtc - nowUtc >= ApprovedCancellationCutoff;
    }

    public bool CancelByCustomer(DateTimeOffset nowUtc)
    {
        if (!CanBeCancelledByCustomer(nowUtc))
        {
            return false;
        }

        Status = AppointmentStatus.Cancelled;
        return true;
    }

    public bool CanApprovedAppointmentBeCancelled(DateTimeOffset nowUtc)
    {
        return Status == AppointmentStatus.Approved
            && StartsAtUtc - nowUtc >= ApprovedCancellationCutoff;
    }

    public bool CancelApprovedAppointment(DateTimeOffset nowUtc)
    {
        if (!CanApprovedAppointmentBeCancelled(nowUtc))
        {
            return false;
        }

        Status = AppointmentStatus.Cancelled;
        return true;
    }
}
