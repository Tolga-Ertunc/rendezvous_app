namespace Rendezvous.Domain.Businesses;

public class BusinessReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerInitial { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsPublic { get; set; } = true;
}
