namespace Rendezvous.Domain.Businesses;

public class BusinessPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string AltText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
