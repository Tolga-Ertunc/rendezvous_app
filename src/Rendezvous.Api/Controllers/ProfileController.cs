using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ProfilePhotoStorageService profilePhotoStorageService;

    public ProfileController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ProfilePhotoStorageService profilePhotoStorageService)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.profilePhotoStorageService = profilePhotoStorageService;
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfilePhotoStorageService.MaxUploadRequestSizeBytes)]
    public async Task<ActionResult<CurrentUserResponse>> UploadPhoto(
        [FromForm] ProfilePhotoUploadRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request.File is null)
        {
            return BadRequest(new { message = "Photo file is required." });
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var previousStorageKey = user.ProfilePhotoStorageKey;
        var profilePhotoId = Guid.NewGuid();
        StoredProfilePhoto? storedPhoto = null;

        try
        {
            storedPhoto = await profilePhotoStorageService.SaveAsync(
                user.Id,
                profilePhotoId,
                request.File,
                cancellationToken);

            user.ProfilePhotoId = profilePhotoId;
            user.ProfilePhotoStorageKey = storedPhoto.StorageKey;
            user.ProfilePhotoContentType = storedPhoto.ContentType;
            user.ProfilePhotoFileSizeBytes = storedPhoto.FileSizeBytes;
            user.ProfilePhotoUpdatedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ProfilePhotoValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch
        {
            if (storedPhoto is not null)
            {
                profilePhotoStorageService.Delete(storedPhoto.StorageKey);
            }

            throw;
        }

        profilePhotoStorageService.Delete(previousStorageKey);

        return Ok(await BuildCurrentUserResponseAsync(user, cancellationToken));
    }

    private async Task<CurrentUserResponse> BuildCurrentUserResponseAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var membershipRows = await dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == user.Id
                && membership.Status == BusinessMembershipStatus.Active)
            .Join(
                dbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
                (membership, business) => new
                {
                    BusinessId = business.Id,
                    BusinessName = business.Name,
                    membership.Role,
                    membership.Status
                })
            .OrderBy(membership => membership.BusinessName)
            .ToListAsync(cancellationToken);

        var memberships = membershipRows
            .Select(membership => new CurrentUserBusinessMembershipResponse(
                membership.BusinessId,
                membership.BusinessName,
                membership.Role.ToString(),
                membership.Status.ToString()))
            .ToList();

        return new CurrentUserResponse(
            user.Id,
            user.PublicNumber,
            user.Email ?? string.Empty,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            UserNames.FormatFullName(user.FirstName, user.LastName),
            ProfilePhotoUrls.Build(user.ProfilePhotoId),
            roles.OrderBy(role => role).ToList(),
            memberships);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed class ProfilePhotoUploadRequest
{
    public IFormFile? File { get; init; }
}
