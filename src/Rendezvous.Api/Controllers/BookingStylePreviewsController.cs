using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
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

    public BookingStylePreviewsController(
        AppDbContext dbContext,
        StylePreviewGenerationService stylePreviewGenerationService)
    {
        this.dbContext = dbContext;
        this.stylePreviewGenerationService = stylePreviewGenerationService;
    }

    [HttpPost]
    [RequestSizeLimit(StylePreviewGenerationService.MaxUploadRequestSizeBytes)]
    public async Task<ActionResult<BookingStylePreviewResponse>> Create(
        [FromForm] CreateBookingStylePreviewRequest request,
        CancellationToken cancellationToken)
    {
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

            return Ok(new BookingStylePreviewResponse(
                generated.PreviewId,
                generated.ImageUrl,
                generated.Prompt,
                generated.IsPlaceholder));
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
    string ImageUrl,
    string Prompt,
    bool IsPlaceholder);
