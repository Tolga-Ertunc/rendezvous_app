using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Route("api/public/profile-photos")]
public class PublicProfilePhotosController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly ProfilePhotoStorageService profilePhotoStorageService;

    public PublicProfilePhotosController(
        AppDbContext dbContext,
        ProfilePhotoStorageService profilePhotoStorageService)
    {
        this.dbContext = dbContext;
        this.profilePhotoStorageService = profilePhotoStorageService;
    }

    [HttpGet("{profilePhotoId:guid}/content")]
    public async Task<IActionResult> GetContent(Guid profilePhotoId, CancellationToken cancellationToken)
    {
        var photo = await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.ProfilePhotoId == profilePhotoId
                && user.ProfilePhotoStorageKey != null
                && user.ProfilePhotoContentType != null)
            .Select(user => new
            {
                StorageKey = user.ProfilePhotoStorageKey!,
                ContentType = user.ProfilePhotoContentType!
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return NotFound();
        }

        var absolutePath = profilePhotoStorageService.GetAbsolutePath(photo.StorageKey);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound();
        }

        return PhysicalFile(absolutePath, photo.ContentType);
    }
}
