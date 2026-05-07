using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner/businesses/{businessId:guid}/photos")]
public class OwnerBusinessPhotosController : ControllerBase
{
    private const int MaxPhotoCount = 4;
    private readonly AppDbContext dbContext;
    private readonly BusinessPhotoStorageService photoStorageService;

    public OwnerBusinessPhotosController(
        AppDbContext dbContext,
        BusinessPhotoStorageService photoStorageService)
    {
        this.dbContext = dbContext;
        this.photoStorageService = photoStorageService;
    }

    [HttpPost]
    [RequestSizeLimit(BusinessPhotoStorageService.MaxFileSizeBytes + 1024)]
    public async Task<ActionResult<OwnerBusinessPhotoResponse>> Upload(
        Guid businessId,
        [FromForm] IFormFile file,
        [FromForm] string? altText,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var currentPhotoCount = await dbContext.BusinessPhotos
            .CountAsync(photo => photo.BusinessId == businessId, cancellationToken);

        if (currentPhotoCount >= MaxPhotoCount)
        {
            return BadRequest(new { message = "A business can have up to 4 photos." });
        }

        var photo = new BusinessPhoto
        {
            BusinessId = businessId,
            AltText = string.IsNullOrWhiteSpace(altText) ? string.Empty : altText.Trim(),
            SortOrder = currentPhotoCount
        };

        try
        {
            var storedPhoto = await photoStorageService.SaveAsync(
                businessId,
                photo.Id,
                file,
                cancellationToken);

            photo.StorageKey = storedPhoto.StorageKey;
            photo.ContentType = storedPhoto.ContentType;
            photo.FileSizeBytes = storedPhoto.FileSizeBytes;
            photo.ImageUrl = $"/backend-api/public/business-photos/{photo.Id}/content";
        }
        catch (BusinessPhotoValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        dbContext.BusinessPhotos.Add(photo);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/owner/businesses/{businessId}/photos/{photo.Id}",
            Map(photo));
    }

    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> Delete(
        Guid businessId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var photo = await dbContext.BusinessPhotos
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == businessId && candidate.Id == photoId,
                cancellationToken);

        if (photo is null)
        {
            return NotFound();
        }

        dbContext.BusinessPhotos.Remove(photo);
        await dbContext.SaveChangesAsync(cancellationToken);
        photoStorageService.Delete(photo.StorageKey);
        await NormalizeSortOrderAsync(businessId, cancellationToken);

        return NoContent();
    }

    [HttpPut("order")]
    public async Task<ActionResult<IReadOnlyList<OwnerBusinessPhotoResponse>>> Reorder(
        Guid businessId,
        OwnerBusinessPhotoOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var photos = await dbContext.BusinessPhotos
            .Where(photo => photo.BusinessId == businessId)
            .ToListAsync(cancellationToken);

        if (request.PhotoIds.Count != photos.Count || request.PhotoIds.Distinct().Count() != request.PhotoIds.Count)
        {
            return BadRequest(new { message = "Photo order must include each business photo exactly once." });
        }

        var photosById = photos.ToDictionary(photo => photo.Id);
        for (var index = 0; index < request.PhotoIds.Count; index++)
        {
            if (!photosById.TryGetValue(request.PhotoIds[index], out var photo))
            {
                return BadRequest(new { message = "Photo order includes an unknown photo." });
            }

            photo.SortOrder = index;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return photos
            .OrderBy(photo => photo.SortOrder)
            .Select(Map)
            .ToList();
    }

    private async Task NormalizeSortOrderAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var photos = await dbContext.BusinessPhotos
            .Where(photo => photo.BusinessId == businessId)
            .OrderBy(photo => photo.SortOrder)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < photos.Count; index++)
        {
            photos[index].SortOrder = index;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> HasActiveOwnerMembershipAsync(
        Guid businessId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.BusinessMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.BusinessId == businessId
                    && membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Owner
                    && membership.Status == BusinessMembershipStatus.Active,
                cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private static OwnerBusinessPhotoResponse Map(BusinessPhoto photo)
    {
        return new OwnerBusinessPhotoResponse(
            photo.Id,
            photo.ImageUrl,
            photo.AltText,
            photo.SortOrder,
            photo.ContentType,
            photo.FileSizeBytes);
    }
}

public sealed record OwnerBusinessPhotoOrderRequest(IReadOnlyList<Guid> PhotoIds);
