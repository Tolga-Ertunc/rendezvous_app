using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Route("api/public/businesses")]
public class PublicBusinessesController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public PublicBusinessesController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PublicBusinessSummaryResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] BusinessType? type,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Businesses
            .AsNoTracking()
            .Where(business => business.Status == BusinessStatus.Approved);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(business => business.Name.ToLower().Contains(normalizedSearch));
        }

        if (type is not null)
        {
            query = query.Where(business => business.Type == type);
        }

        var businesses = await query
            .OrderBy(business => business.Name)
            .Select(business => new
            {
                business.Id,
                business.Name,
                business.Type,
                business.TimeZoneId
            })
            .ToListAsync(cancellationToken);

        var businessIds = businesses.Select(business => business.Id).ToList();
        var serviceRows = await dbContext.BusinessServices
            .AsNoTracking()
            .Where(service => businessIds.Contains(service.BusinessId) && service.IsActive)
            .OrderBy(service => service.Name)
            .Select(service => new
            {
                service.BusinessId,
                service.Id,
                service.Name,
                service.DurationMinutes,
                service.CurrencyCode
            })
            .ToListAsync(cancellationToken);

        var servicesByBusinessId = serviceRows
            .GroupBy(service => service.BusinessId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicBusinessSummaryServiceResponse>)group
                    .Select(service => new PublicBusinessSummaryServiceResponse(
                        service.Id,
                        service.Name,
                        service.DurationMinutes,
                        service.CurrencyCode))
                    .ToList());

        return businesses
            .Select(business => new PublicBusinessSummaryResponse(
                business.Id,
                business.Name,
                business.Type.ToString(),
                business.TimeZoneId,
                servicesByBusinessId.GetValueOrDefault(business.Id, [])))
            .ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicBusinessDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .AsNoTracking()
            .Where(candidate => candidate.Id == id && candidate.Status == BusinessStatus.Approved)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Type,
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

        var services = await dbContext.BusinessServices
            .AsNoTracking()
            .Where(service => service.BusinessId == id && service.IsActive)
            .OrderBy(service => service.Name)
            .Select(service => new PublicBusinessServiceResponse(
                service.Id,
                service.Name,
                service.CategoryName,
                service.DurationMinutes,
                service.BasePriceAmount,
                service.CurrencyCode))
            .ToListAsync(cancellationToken);

        var workingHours = await dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(workingHour => workingHour.BusinessId == id)
            .OrderBy(workingHour => workingHour.DayOfWeek)
            .Select(workingHour => new PublicBusinessWorkingHourResponse(
                workingHour.DayOfWeek.ToString(),
                workingHour.OpensAt.ToString("HH:mm"),
                workingHour.ClosesAt.ToString("HH:mm")))
            .ToListAsync(cancellationToken);

        var staffMembers = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staffMember => staffMember.BusinessId == id && staffMember.IsActive)
            .OrderBy(staffMember => staffMember.DisplayName)
            .Select(staffMember => new PublicBusinessStaffMemberResponse(
                staffMember.Id,
                staffMember.DisplayName))
            .ToListAsync(cancellationToken);

        var photos = await dbContext.BusinessPhotos
            .AsNoTracking()
            .Where(photo => photo.BusinessId == id)
            .OrderBy(photo => photo.SortOrder)
            .Select(photo => new PublicBusinessPhotoResponse(
                photo.Id,
                photo.ImageUrl,
                photo.AltText,
                photo.SortOrder))
            .ToListAsync(cancellationToken);

        var reviews = await dbContext.BusinessReviews
            .AsNoTracking()
            .Where(review => review.BusinessId == id && review.IsPublic)
            .OrderByDescending(review => review.CreatedAtUtc)
            .Select(review => new PublicBusinessReviewResponse(
                review.Id,
                review.CustomerName,
                review.CustomerInitial,
                review.Rating,
                review.Comment,
                review.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var reviewSummary = reviews.Count == 0
            ? new PublicBusinessReviewSummaryResponse(0, 0)
            : new PublicBusinessReviewSummaryResponse(
                Math.Round(reviews.Average(review => review.Rating), 1),
                reviews.Count);

        return new PublicBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.TimeZoneId,
            new PublicBusinessAddressResponse(
                business.AddressLine,
                business.District,
                business.City,
                business.Country),
            business.Description,
            services,
            workingHours,
            staffMembers,
            photos,
            reviewSummary,
            reviews,
            CreateAdditionalInformation(business.SupportsInstantConfirmation,
                business.SupportsPayByApp,
                business.IsPetFriendly,
                business.IsKidFriendly,
                business.IsNearPublicTransport,
                business.UsesOrganicProducts,
                business.UsesVeganProducts,
                business.IsEnvironmentallyFriendly));
    }

    private static IReadOnlyList<string> CreateAdditionalInformation(
        bool supportsInstantConfirmation,
        bool supportsPayByApp,
        bool isPetFriendly,
        bool isKidFriendly,
        bool isNearPublicTransport,
        bool usesOrganicProducts,
        bool usesVeganProducts,
        bool isEnvironmentallyFriendly)
    {
        var items = new List<string>();

        if (supportsInstantConfirmation)
        {
            items.Add("Instant Confirmation");
        }

        if (supportsPayByApp)
        {
            items.Add("Pay by app");
        }

        if (isPetFriendly)
        {
            items.Add("Pet-friendly");
        }

        if (isKidFriendly)
        {
            items.Add("Kid-friendly");
        }

        if (isNearPublicTransport)
        {
            items.Add("Near public transport");
        }

        if (usesOrganicProducts)
        {
            items.Add("Organic products only");
        }

        if (usesVeganProducts)
        {
            items.Add("Vegan products only");
        }

        if (isEnvironmentallyFriendly)
        {
            items.Add("Environmentally friendly");
        }

        return items;
    }
}

public sealed record PublicBusinessSummaryResponse(
    Guid Id,
    string Name,
    string Type,
    string TimeZoneId,
    IReadOnlyList<PublicBusinessSummaryServiceResponse> Services);

public sealed record PublicBusinessSummaryServiceResponse(
    Guid Id,
    string Name,
    int DurationMinutes,
    string CurrencyCode);

public sealed record PublicBusinessDetailResponse(
    Guid Id,
    string Name,
    string Type,
    string TimeZoneId,
    PublicBusinessAddressResponse Address,
    string Description,
    IReadOnlyList<PublicBusinessServiceResponse> Services,
    IReadOnlyList<PublicBusinessWorkingHourResponse> WorkingHours,
    IReadOnlyList<PublicBusinessStaffMemberResponse> StaffMembers,
    IReadOnlyList<PublicBusinessPhotoResponse> Photos,
    PublicBusinessReviewSummaryResponse ReviewSummary,
    IReadOnlyList<PublicBusinessReviewResponse> Reviews,
    IReadOnlyList<string> AdditionalInformation);

public sealed record PublicBusinessServiceResponse(
    Guid Id,
    string Name,
    string CategoryName,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode);

public sealed record PublicBusinessAddressResponse(
    string AddressLine,
    string District,
    string City,
    string Country);

public sealed record PublicBusinessWorkingHourResponse(
    string DayOfWeek,
    string OpensAt,
    string ClosesAt);

public sealed record PublicBusinessStaffMemberResponse(
    Guid Id,
    string DisplayName);

public sealed record PublicBusinessPhotoResponse(
    Guid Id,
    string ImageUrl,
    string AltText,
    int SortOrder);

public sealed record PublicBusinessReviewSummaryResponse(
    decimal AverageRating,
    int ReviewCount);

public sealed record PublicBusinessReviewResponse(
    Guid Id,
    string CustomerName,
    string CustomerInitial,
    decimal Rating,
    string Comment,
    DateTimeOffset CreatedAtUtc);
