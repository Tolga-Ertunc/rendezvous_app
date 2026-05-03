namespace Rendezvous.Domain.Services;

public class BusinessService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal BasePriceAmount { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public bool IsActive { get; set; } = true;
}
