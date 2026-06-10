using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.User)]
[Route("api/booking/style-previews")]
public class BookingStylePreviewsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly StylePreviewGenerationService stylePreviewGenerationService;
    private readonly AppointmentStylePreviewStorageService stylePreviewStorageService;

    public BookingStylePreviewsController(
        AppDbContext dbContext,
        StylePreviewGenerationService stylePreviewGenerationService,
        AppointmentStylePreviewStorageService stylePreviewStorageService)
    {
        this.dbContext = dbContext;
        this.stylePreviewGenerationService = stylePreviewGenerationService;
        this.stylePreviewStorageService = stylePreviewStorageService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(StylePreviewGenerationService.MaxUploadRequestSizeBytes)]
    public async Task<ActionResult<BookingStylePreviewResponse>> Create(
        [FromForm] CreateBookingStylePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var customerUserId = GetCurrentUserId();
        if (customerUserId is null)
        {
            return Unauthorized();
        }

        if (request.Image is null)
        {
            return BadRequest(new { message = "Photo file is required." });
        }

        var canPreview = await dbContext.Businesses
            .AsNoTracking()
            .Where(business =>
                business.Id == request.BusinessId
                && business.Status == BusinessStatus.Approved)
            .AnyAsync(cancellationToken);

        if (!canPreview)
        {
            return NotFound();
        }

        var serviceExists = await dbContext.BusinessServices
            .AsNoTracking()
            .AnyAsync(service =>
                service.Id == request.ServiceId
                && service.BusinessId == request.BusinessId
                && service.IsActive,
                cancellationToken);

        var staffExists = await dbContext.StaffMembers
            .AsNoTracking()
            .AnyAsync(staffMember =>
                staffMember.Id == request.StaffMemberId
                && staffMember.BusinessId == request.BusinessId
                && staffMember.IsActive,
                cancellationToken);

        if (!serviceExists || !staffExists)
        {
            return NotFound();
        }

        try
        {
            var generated = await stylePreviewGenerationService.GenerateAsync(
                request.Image,
                request.Prompt,
                cancellationToken);

            var stored = await stylePreviewStorageService.SaveAsync(
                generated.PreviewId,
                request.Image,
                generated.GeneratedImage,
                cancellationToken);

            var nowUtc = DateTimeOffset.UtcNow;
            var preview = new AppointmentStylePreview
            {
                Id = generated.PreviewId,
                CustomerUserId = customerUserId.Value,
                BusinessId = request.BusinessId,
                BusinessServiceId = request.ServiceId,
                StaffMemberId = request.StaffMemberId,
                OriginalStorageKey = stored.OriginalStorageKey,
                OriginalContentType = stored.OriginalContentType,
                OriginalFileSizeBytes = stored.OriginalFileSizeBytes,
                GeneratedStorageKey = stored.GeneratedStorageKey,
                GeneratedContentType = stored.GeneratedContentType,
                GeneratedFileSizeBytes = stored.GeneratedFileSizeBytes,
                IsPlaceholder = generated.IsPlaceholder,
                CreatedAtUtc = nowUtc,
                ExpiresAtUtc = nowUtc.AddHours(24)
            };

            dbContext.AppointmentStylePreviews.Add(preview);
            await dbContext.SaveChangesAsync(cancellationToken);

            var originalImageUrl = BuildImageUrl(preview.Id, "original");
            var generatedImageUrl = BuildImageUrl(preview.Id, "generated");

            return Ok(new BookingStylePreviewResponse(
                preview.Id,
                originalImageUrl,
                generatedImageUrl,
                generatedImageUrl,
                preview.IsPlaceholder));
        }
        catch (StylePreviewValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (StylePreviewConfigurationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (StylePreviewGenerationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Style preview could not be generated." });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private static string BuildImageUrl(Guid previewId, string imageKind)
    {
        return $"/backend-api/appointment-style-previews/{previewId}/{imageKind}";
    }
}

public sealed class CreateBookingStylePreviewRequest
{
    public Guid BusinessId { get; init; }
    public Guid ServiceId { get; init; }
    public Guid StaffMemberId { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public IFormFile? Image { get; init; }
}

public sealed record BookingStylePreviewResponse(
    Guid PreviewId,
    string OriginalImageUrl,
    string GeneratedImageUrl,
    string ImageUrl,
    bool IsPlaceholder);
