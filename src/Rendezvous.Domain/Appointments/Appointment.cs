namespace Rendezvous.Domain.Appointments;

public class Appointment
{
    private static readonly TimeSpan ApprovedCancellationCutoff = TimeSpan.FromHours(1);

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
