namespace Rendezvous.Domain.Availability;

public class AvailabilityException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid? StaffMemberId { get; set; }
    public AvailabilityExceptionType Type { get; set; }
    public DateOnly Date { get; set; }
    public bool IsFullDay { get; set; }
    public TimeOnly? StartsAt { get; set; }
    public TimeOnly? EndsAt { get; set; }
    public string? Note { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
