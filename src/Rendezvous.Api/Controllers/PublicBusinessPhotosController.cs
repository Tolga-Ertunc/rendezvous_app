using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Route("api/public/business-photos")]
public class PublicBusinessPhotosController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly BusinessPhotoStorageService photoStorageService;

    public PublicBusinessPhotosController(
        AppDbContext dbContext,
        BusinessPhotoStorageService photoStorageService)
    {
        this.dbContext = dbContext;
        this.photoStorageService = photoStorageService;
    }

    [HttpGet("{photoId:guid}/content")]
    public async Task<IActionResult> GetContent(Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await dbContext.BusinessPhotos
            .AsNoTracking()
            .Where(candidate => candidate.Id == photoId)
            .Select(candidate => new
            {
                candidate.StorageKey,
                candidate.ContentType
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return NotFound();
        }

        var absolutePath = photoStorageService.GetAbsolutePath(photo.StorageKey);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound();
        }

        return PhysicalFile(absolutePath, photo.ContentType);
    }
}
