namespace Rendezvous.Domain.Availability;

public class BusinessWorkingHour
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}
