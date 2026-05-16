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
[Route("api/owner/businesses")]
public class OwnerBusinessesController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly BusinessProvisioningService businessProvisioningService;

    public OwnerBusinessesController(
        AppDbContext dbContext,
        BusinessProvisioningService businessProvisioningService)
    {
        this.dbContext = dbContext;
        this.businessProvisioningService = businessProvisioningService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerBusinessSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var businessRows = await GetOwnedBusinessQuery(userId.Value)
            .OrderBy(business => business.Name)
            .Select(business => new
            {
                business.Id,
                business.Name,
                business.Type,
                business.Status,
                business.TimeZoneId
            })
            .ToListAsync(cancellationToken);

        return businessRows
            .Select(business => new OwnerBusinessSummaryResponse(
                business.Id,
                business.Name,
                business.Type.ToString(),
                business.Status.ToString(),
                business.TimeZoneId))
            .ToList();
    }

    [HttpPost]
    public async Task<ActionResult<OwnerBusinessDetailResponse>> Create(
        CreateOwnerBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Business name is required." });
        }

        var hasOwnerAccess = await dbContext.BusinessMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == userId.Value
                    && membership.Role == BusinessMembershipRole.Owner
                    && membership.Status == BusinessMembershipStatus.Active,
                cancellationToken);

        if (!hasOwnerAccess)
        {
            return Forbid();
        }

        var business = await businessProvisioningService.CreateOwnedBusinessAsync(
            userId.Value,
            request.Name,
            request.Type,
            request.OwnerStaffDisplayName ?? string.Empty,
            BusinessStatus.PendingApproval,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var staffMember = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(candidate => candidate.BusinessId == business.Id && candidate.UserId == userId.Value)
            .SingleAsync(cancellationToken);
        var serviceCategories = await dbContext.BusinessServiceCategories
            .AsNoTracking()
            .Where(category => category.BusinessId == business.Id)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new OwnerBusinessServiceCategoryResponse(
                category.Id,
                category.Name,
                category.SortOrder,
                category.IsSystem))
            .ToListAsync(cancellationToken);

        var response = new OwnerBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.Status.ToString(),
            business.TimeZoneId,
            business.AddressLine,
            business.District,
            business.City,
            business.Country,
            business.Description,
            business.SupportsInstantConfirmation,
            business.SupportsPayByApp,
            business.IsPetFriendly,
            business.IsKidFriendly,
            business.IsNearPublicTransport,
            business.UsesOrganicProducts,
            business.UsesVeganProducts,
            business.IsEnvironmentallyFriendly,
            serviceCategories,
            [],
            [
                new OwnerBusinessStaffMemberResponse(
                    staffMember.Id,
                    staffMember.DisplayName,
                    staffMember.IsActive)
            ],
            [],
            new OwnerBusinessReviewSummaryResponse(0, 0),
            []);

        return Created($"/api/owner/businesses/{business.Id}", response);
    }

    [HttpGet("{businessId:guid}")]
    public async Task<ActionResult<OwnerBusinessDetailResponse>> Get(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var business = await GetOwnedBusinessQuery(userId.Value)
            .Where(candidate => candidate.Id == businessId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Type,
                candidate.Status,
                candidate.TimeZoneId,
                candidate.AddressLine,
                candidate.District,
                candidate.City,
                candidate.Country,
                candidate.Description,
                candidate.SupportsInstantConfirmation,
                candidate.SupportsPayByApp,
                candidate.IsPetFriendly,
                candidate.IsKidFriendly,
                candidate.IsNearPublicTransport,
                candidate.UsesOrganicProducts,
                candidate.UsesVeganProducts,
                candidate.IsEnvironmentallyFriendly
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return NotFound();
        }

        await EnsureFeaturedCategoryAsync(businessId, cancellationToken);

        var serviceCategories = await dbContext.BusinessServiceCategories
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

        var services = await dbContext.BusinessServices
            .AsNoTracking()
            .Where(service => service.BusinessId == businessId)
            .OrderBy(service => service.Name)
            .Select(service => new OwnerBusinessServiceResponse(
                service.Id,
                service.Name,
                service.CategoryName,
                service.Description,
                service.DurationMinutes,
                service.BasePriceAmount,
                service.CurrencyCode,
                service.IsActive))
            .ToListAsync(cancellationToken);

        var staffMembers = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staffMember => staffMember.BusinessId == businessId)
            .OrderBy(staffMember => staffMember.DisplayName)
            .Select(staffMember => new OwnerBusinessStaffMemberResponse(
                staffMember.Id,
                staffMember.DisplayName,
                staffMember.IsActive))
            .ToListAsync(cancellationToken);

        var photos = await dbContext.BusinessPhotos
            .AsNoTracking()
            .Where(photo => photo.BusinessId == businessId)
            .OrderBy(photo => photo.SortOrder)
            .Select(photo => new OwnerBusinessPhotoResponse(
                photo.Id,
                photo.ImageUrl,
                photo.AltText,
                photo.SortOrder,
                photo.ContentType,
                photo.FileSizeBytes))
            .ToListAsync(cancellationToken);

        var reviews = await dbContext.BusinessReviews
            .AsNoTracking()
            .Where(review => review.BusinessId == businessId && review.IsPublic)
            .OrderByDescending(review => review.CreatedAtUtc)
            .Select(review => new OwnerBusinessReviewResponse(
                review.Id,
                review.CustomerName,
                review.CustomerInitial,
                review.Rating,
                review.Comment,
                review.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var reviewSummary = reviews.Count == 0
            ? new OwnerBusinessReviewSummaryResponse(0, 0)
            : new OwnerBusinessReviewSummaryResponse(
                Math.Round(reviews.Average(review => review.Rating), 1),
                reviews.Count);

        return new OwnerBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.Status.ToString(),
            business.TimeZoneId,
            business.AddressLine,
            business.District,
            business.City,
            business.Country,
            business.Description,
            business.SupportsInstantConfirmation,
            business.SupportsPayByApp,
            business.IsPetFriendly,
            business.IsKidFriendly,
            business.IsNearPublicTransport,
            business.UsesOrganicProducts,
            business.UsesVeganProducts,
            business.IsEnvironmentallyFriendly,
            serviceCategories,
            services,
            staffMembers,
            photos,
            reviewSummary,
            reviews);
    }

    [HttpPut("{businessId:guid}/profile")]
    public async Task<ActionResult<OwnerBusinessDetailResponse>> UpdateProfile(
        Guid businessId,
        OwnerBusinessProfileRequest request,
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

        var validationError = ValidateProfileRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var business = await dbContext.Businesses
            .SingleOrDefaultAsync(candidate => candidate.Id == businessId, cancellationToken);

        if (business is null)
        {
            return NotFound();
        }

        business.Name = request.Name.Trim();
        business.TimeZoneId = request.TimeZoneId.Trim();
        business.AddressLine = request.AddressLine.Trim();
        business.District = request.District.Trim();
        business.City = request.City.Trim();
        business.Country = request.Country.Trim();
        business.Description = request.Description.Trim();
        business.SupportsInstantConfirmation = request.SupportsInstantConfirmation;
        business.SupportsPayByApp = request.SupportsPayByApp;
        business.IsPetFriendly = request.IsPetFriendly;
        business.IsKidFriendly = request.IsKidFriendly;
        business.IsNearPublicTransport = request.IsNearPublicTransport;
        business.UsesOrganicProducts = request.UsesOrganicProducts;
        business.UsesVeganProducts = request.UsesVeganProducts;
        business.IsEnvironmentallyFriendly = request.IsEnvironmentallyFriendly;

        await dbContext.SaveChangesAsync(cancellationToken);

        var detail = await Get(businessId, cancellationToken);

        return detail.Value is null ? NotFound() : detail.Value;
    }

    private IQueryable<Business> GetOwnedBusinessQuery(Guid userId)
    {
        return dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == userId
                && membership.Role == BusinessMembershipRole.Owner
                && membership.Status == BusinessMembershipStatus.Active)
            .Join(
                dbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
                (_, business) => business);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
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

        dbContext.BusinessServiceCategories.Add(new Domain.Services.BusinessServiceCategory
        {
            BusinessId = businessId,
            Name = "Featured",
            SortOrder = 0,
            IsSystem = true
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? ValidateProfileRequest(OwnerBusinessProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Business name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            return "Timezone is required.";
        }

        if (!IsValidTimeZone(request.TimeZoneId.Trim()))
        {
            return "Timezone is invalid.";
        }

        if (request.Description.Length > 1200)
        {
            return "Description cannot exceed 1200 characters.";
        }

        return null;
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

}

public sealed record CreateOwnerBusinessRequest(
    string Name,
    BusinessType Type,
    string? OwnerStaffDisplayName);

public sealed record OwnerBusinessSummaryResponse(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string TimeZoneId);

public sealed record OwnerBusinessDetailResponse(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string TimeZoneId,
    string AddressLine,
    string District,
    string City,
    string Country,
    string Description,
    bool SupportsInstantConfirmation,
    bool SupportsPayByApp,
    bool IsPetFriendly,
    bool IsKidFriendly,
    bool IsNearPublicTransport,
    bool UsesOrganicProducts,
    bool UsesVeganProducts,
    bool IsEnvironmentallyFriendly,
    IReadOnlyList<OwnerBusinessServiceCategoryResponse> ServiceCategories,
    IReadOnlyList<OwnerBusinessServiceResponse> Services,
    IReadOnlyList<OwnerBusinessStaffMemberResponse> StaffMembers,
    IReadOnlyList<OwnerBusinessPhotoResponse> Photos,
    OwnerBusinessReviewSummaryResponse ReviewSummary,
    IReadOnlyList<OwnerBusinessReviewResponse> Reviews);

public sealed record OwnerBusinessServiceResponse(
    Guid Id,
    string Name,
    string CategoryName,
    string Description,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode,
    bool IsActive);

public sealed record OwnerBusinessStaffMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive);

public sealed record OwnerBusinessProfileRequest(
    string Name,
    string TimeZoneId,
    string AddressLine,
    string District,
    string City,
    string Country,
    string Description,
    bool SupportsInstantConfirmation,
    bool SupportsPayByApp,
    bool IsPetFriendly,
    bool IsKidFriendly,
    bool IsNearPublicTransport,
    bool UsesOrganicProducts,
    bool UsesVeganProducts,
    bool IsEnvironmentallyFriendly);

public sealed record OwnerBusinessPhotoResponse(
    Guid Id,
    string ImageUrl,
    string AltText,
    int SortOrder,
    string ContentType,
    long FileSizeBytes);

public sealed record OwnerBusinessReviewSummaryResponse(
    decimal AverageRating,
    int ReviewCount);

public sealed record OwnerBusinessReviewResponse(
    Guid Id,
    string CustomerName,
    string CustomerInitial,
    decimal Rating,
    string Comment,
    DateTimeOffset CreatedAtUtc);
