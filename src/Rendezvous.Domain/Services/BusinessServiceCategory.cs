namespace Rendezvous.Domain.Services;

public class BusinessServiceCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = "Featured";
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}
