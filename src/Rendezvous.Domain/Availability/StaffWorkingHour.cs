namespace Rendezvous.Domain.Availability;

public class StaffWorkingHour
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffMemberId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
}
