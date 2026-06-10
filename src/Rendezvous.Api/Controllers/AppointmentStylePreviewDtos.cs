namespace Rendezvous.Api.Controllers;

public sealed record AppointmentStylePreviewResponse(
    Guid Id,
    string OriginalImageUrl,
    string GeneratedImageUrl,
    bool IsPlaceholder);
