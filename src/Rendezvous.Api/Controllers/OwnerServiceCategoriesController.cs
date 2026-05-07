using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner/businesses/{businessId:guid}/service-categories")]
public class OwnerServiceCategoriesController : ControllerBase
{
    private const string FeaturedCategoryName = "Featured";
    private readonly AppDbContext dbContext;

    public OwnerServiceCategoriesController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerBusinessServiceCategoryResponse>>> List(
        Guid businessId,
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

        await EnsureFeaturedCategoryAsync(businessId, cancellationToken);

        return await dbContext.BusinessServiceCategories
            .AsNoTracking()
            .Where(category => category.BusinessId == businessId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new OwnerBusinessServiceCategoryResponse(
                category.Id,
                category.Name,
                category.SortOrder,
                category.IsSystem))
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<OwnerBusinessServiceCategoryResponse>> Create(
        Guid businessId,
        OwnerServiceCategoryRequest request,
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

        var name = NormalizeCategoryName(request.Name);
        var validationError = ValidateCustomCategoryName(name);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        await EnsureFeaturedCategoryAsync(businessId, cancellationToken);

        if (await CategoryExistsAsync(businessId, name, cancellationToken))
        {
            return BadRequest(new { message = "Category already exists." });
        }

        var sortOrder = await dbContext.BusinessServiceCategories
            .Where(category => category.BusinessId == businessId)
            .Select(category => (int?)category.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var category = new BusinessServiceCategory
        {
            BusinessId = businessId,
            Name = name,
            SortOrder = sortOrder + 1,
            IsSystem = false
        };

        dbContext.BusinessServiceCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/owner/businesses/{businessId}/service-categories/{category.Id}",
            Map(category));
    }

    [HttpPut("{categoryId:guid}")]
    public async Task<ActionResult<OwnerBusinessServiceCategoryResponse>> Update(
        Guid businessId,
        Guid categoryId,
        OwnerServiceCategoryRequest request,
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

        var category = await dbContext.BusinessServiceCategories
            .SingleOrDefaultAsync(
                candidate => candidate.Id == categoryId && candidate.BusinessId == businessId,
                cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        if (category.IsSystem)
        {
            return BadRequest(new { message = "Featured category cannot be renamed." });
        }

        var name = NormalizeCategoryName(request.Name);
        var validationError = ValidateCustomCategoryName(name);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        if (await dbContext.BusinessServiceCategories.AnyAsync(
                candidate =>
                    candidate.BusinessId == businessId
                    && candidate.Id != categoryId
                    && candidate.Name == name,
                cancellationToken))
        {
            return BadRequest(new { message = "Category already exists." });
        }

        var oldName = category.Name;
        category.Name = name;

        var services = await dbContext.BusinessServices
            .Where(service => service.BusinessId == businessId && service.CategoryName == oldName)
            .ToListAsync(cancellationToken);
        foreach (var service in services)
        {
            service.CategoryName = name;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(category);
    }

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Delete(
        Guid businessId,
        Guid categoryId,
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

        var category = await dbContext.BusinessServiceCategories
            .SingleOrDefaultAsync(
                candidate => candidate.Id == categoryId && candidate.BusinessId == businessId,
                cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        if (category.IsSystem)
        {
            return BadRequest(new { message = "Featured category cannot be deleted." });
        }

        if (await dbContext.BusinessServices.AnyAsync(
                service =>
                    service.BusinessId == businessId
                    && service.CategoryName == category.Name,
                cancellationToken))
        {
            return BadRequest(new { message = "Move or deactivate services before deleting this category." });
        }

        dbContext.BusinessServiceCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task EnsureFeaturedCategoryAsync(Guid businessId, CancellationToken cancellationToken)
    {
        if (await dbContext.BusinessServiceCategories.AnyAsync(
                category => category.BusinessId == businessId && category.Name == FeaturedCategoryName,
                cancellationToken))
        {
            return;
        }

        dbContext.BusinessServiceCategories.Add(new BusinessServiceCategory
        {
            BusinessId = businessId,
            Name = FeaturedCategoryName,
            SortOrder = 0,
            IsSystem = true
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> CategoryExistsAsync(
        Guid businessId,
        string name,
        CancellationToken cancellationToken)
    {
        return dbContext.BusinessServiceCategories
            .AsNoTracking()
            .AnyAsync(
                category => category.BusinessId == businessId && category.Name == name,
                cancellationToken);
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

    private static string NormalizeCategoryName(string name)
    {
        return name.Trim();
    }

    private static string? ValidateCustomCategoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Category name is required.";
        }

        if (name.Length > 120)
        {
            return "Category name cannot exceed 120 characters.";
        }

        if (string.Equals(name, FeaturedCategoryName, StringComparison.OrdinalIgnoreCase))
        {
            return "Featured category already exists.";
        }

        return null;
    }

    private static OwnerBusinessServiceCategoryResponse Map(BusinessServiceCategory category)
    {
        return new OwnerBusinessServiceCategoryResponse(
            category.Id,
            category.Name,
            category.SortOrder,
            category.IsSystem);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record OwnerServiceCategoryRequest(string Name);

public sealed record OwnerBusinessServiceCategoryResponse(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsSystem);
