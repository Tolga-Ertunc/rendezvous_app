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
[Route("api/owner/businesses/{businessId:guid}/services")]
public class OwnerServicesController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public OwnerServicesController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpPost]
    public async Task<ActionResult<OwnerServiceMutationResponse>> Create(
        Guid businessId,
        OwnerServiceRequest request,
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

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var categoryName = NormalizeCategoryName(request.CategoryName);
        await EnsureFeaturedCategoryAsync(businessId, cancellationToken);
        if (!await ServiceCategoryExistsAsync(businessId, categoryName, cancellationToken))
        {
            return BadRequest(new { message = "Select an existing service category." });
        }

        var service = new BusinessService
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            CategoryName = categoryName,
            Description = NormalizeDescription(request.Description),
            DurationMinutes = request.DurationMinutes,
            BasePriceAmount = request.BasePriceAmount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            IsActive = request.IsActive
        };

        dbContext.BusinessServices.Add(service);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/owner/businesses/{businessId}/services/{service.Id}",
            Map(service));
    }

    [HttpPut("{serviceId:guid}")]
    public async Task<ActionResult<OwnerServiceMutationResponse>> Update(
        Guid businessId,
        Guid serviceId,
        OwnerServiceRequest request,
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

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var service = await dbContext.BusinessServices
            .SingleOrDefaultAsync(
                candidate => candidate.Id == serviceId && candidate.BusinessId == businessId,
                cancellationToken);

        if (service is null)
        {
            return NotFound();
        }

        var categoryName = NormalizeCategoryName(request.CategoryName);
        await EnsureFeaturedCategoryAsync(businessId, cancellationToken);
        if (!await ServiceCategoryExistsAsync(businessId, categoryName, cancellationToken))
        {
            return BadRequest(new { message = "Select an existing service category." });
        }

        service.Name = request.Name.Trim();
        service.CategoryName = categoryName;
        service.Description = NormalizeDescription(request.Description);
        service.DurationMinutes = request.DurationMinutes;
        service.BasePriceAmount = request.BasePriceAmount;
        service.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        service.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(service);
    }

    [HttpPost("{serviceId:guid}/activate")]
    public Task<ActionResult<OwnerServiceMutationResponse>> Activate(
        Guid businessId,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        return ChangeActiveStateAsync(businessId, serviceId, true, cancellationToken);
    }

    [HttpPost("{serviceId:guid}/deactivate")]
    public Task<ActionResult<OwnerServiceMutationResponse>> Deactivate(
        Guid businessId,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        return ChangeActiveStateAsync(businessId, serviceId, false, cancellationToken);
    }

    private async Task<ActionResult<OwnerServiceMutationResponse>> ChangeActiveStateAsync(
        Guid businessId,
        Guid serviceId,
        bool isActive,
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

        var service = await dbContext.BusinessServices
            .SingleOrDefaultAsync(
                candidate => candidate.Id == serviceId && candidate.BusinessId == businessId,
                cancellationToken);

        if (service is null)
        {
            return NotFound();
        }

        service.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(service);
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

    private async Task EnsureFeaturedCategoryAsync(Guid businessId, CancellationToken cancellationToken)
    {
        if (await dbContext.BusinessServiceCategories.AnyAsync(
                category => category.BusinessId == businessId && category.Name == "Featured",
                cancellationToken))
        {
            return;
        }

        dbContext.BusinessServiceCategories.Add(new BusinessServiceCategory
        {
            BusinessId = businessId,
            Name = "Featured",
            SortOrder = 0,
            IsSystem = true
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> ServiceCategoryExistsAsync(
        Guid businessId,
        string categoryName,
        CancellationToken cancellationToken)
    {
        return dbContext.BusinessServiceCategories
            .AsNoTracking()
            .AnyAsync(
                category => category.BusinessId == businessId && category.Name == categoryName,
                cancellationToken);
    }

    private static string? ValidateRequest(OwnerServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Service name is required.";
        }

        if (request.DurationMinutes <= 0)
        {
            return "Service duration must be greater than zero.";
        }

        if (request.BasePriceAmount < 0)
        {
            return "Service price cannot be negative.";
        }

        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3)
        {
            return "Currency code must be 3 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryName) && request.CategoryName.Trim().Length > 120)
        {
            return "Service category cannot exceed 120 characters.";
        }

        if (request.Description is not null && request.Description.Length > 1200)
        {
            return "Service description cannot exceed 1200 characters.";
        }

        return null;
    }

    private static OwnerServiceMutationResponse Map(BusinessService service)
    {
        return new OwnerServiceMutationResponse(
            service.Id,
            service.Name,
            service.CategoryName,
            service.Description,
            service.DurationMinutes,
            service.BasePriceAmount,
            service.CurrencyCode,
            service.IsActive);
    }

    private static string NormalizeCategoryName(string? categoryName)
    {
        return string.IsNullOrWhiteSpace(categoryName)
            ? "Featured"
            : categoryName.Trim();
    }

    private static string NormalizeDescription(string? description)
    {
        return description?.Trim() ?? string.Empty;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record OwnerServiceRequest(
    string Name,
    string? CategoryName,
    string? Description,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode,
    bool IsActive);

public sealed record OwnerServiceMutationResponse(
    Guid Id,
    string Name,
    string CategoryName,
    string Description,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode,
    bool IsActive);
