using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
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
                business.TimeZoneId,
                business.AddressLine,
                business.District,
                business.City,
                business.Country,
                business.SupportsInstantConfirmation,
                business.SupportsPayByApp,
                business.IsPetFriendly,
                business.IsKidFriendly,
                business.IsNearPublicTransport,
                business.UsesOrganicProducts,
                business.UsesVeganProducts,
                business.IsEnvironmentallyFriendly
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

        var workingHourRows = await dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(workingHour => businessIds.Contains(workingHour.BusinessId))
            .OrderBy(workingHour => workingHour.DayOfWeek)
            .Select(workingHour => new
            {
                workingHour.BusinessId,
                workingHour.DayOfWeek,
                workingHour.OpensAt,
                workingHour.ClosesAt
            })
            .ToListAsync(cancellationToken);

        var workingHoursByBusinessId = workingHourRows
            .GroupBy(workingHour => workingHour.BusinessId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicBusinessWorkingHourResponse>)group
                    .Select(workingHour => new PublicBusinessWorkingHourResponse(
                        workingHour.DayOfWeek.ToString(),
                        workingHour.OpensAt.ToString("HH:mm"),
                        workingHour.ClosesAt.ToString("HH:mm")))
                    .ToList());

        var photoRows = await dbContext.BusinessPhotos
            .AsNoTracking()
            .Where(photo => businessIds.Contains(photo.BusinessId))
            .OrderBy(photo => photo.SortOrder)
            .Select(photo => new
            {
                photo.BusinessId,
                photo.Id,
                photo.ImageUrl,
                photo.AltText,
                photo.SortOrder
            })
            .ToListAsync(cancellationToken);

        var photosByBusinessId = photoRows
            .GroupBy(photo => photo.BusinessId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PublicBusinessPhotoResponse>)group
                    .Select(photo => new PublicBusinessPhotoResponse(
                        photo.Id,
                        photo.ImageUrl,
                        photo.AltText,
                        photo.SortOrder))
                    .ToList());

        var reviewRows = await dbContext.BusinessReviews
            .AsNoTracking()
            .Where(review => businessIds.Contains(review.BusinessId) && review.IsPublic)
            .Select(review => new
            {
                review.BusinessId,
                review.Rating
            })
            .ToListAsync(cancellationToken);

        var reviewSummariesByBusinessId = reviewRows
            .GroupBy(review => review.BusinessId)
            .ToDictionary(
                group => group.Key,
                group => new PublicBusinessReviewSummaryResponse(
                    Math.Round(group.Average(review => review.Rating), 1),
                    group.Count()));

        return businesses
            .Select(business => new PublicBusinessSummaryResponse(
                business.Id,
                business.Name,
                business.Type.ToString(),
                business.TimeZoneId,
                new PublicBusinessAddressResponse(
                    business.AddressLine,
                    business.District,
                    business.City,
                    business.Country),
                servicesByBusinessId.GetValueOrDefault(business.Id, []),
                workingHoursByBusinessId.GetValueOrDefault(business.Id, []),
                photosByBusinessId.GetValueOrDefault(business.Id, []),
                reviewSummariesByBusinessId.GetValueOrDefault(
                    business.Id,
                    new PublicBusinessReviewSummaryResponse(0, 0)),
                CreateAdditionalInformation(business.SupportsInstantConfirmation,
                    business.SupportsPayByApp,
                    business.IsPetFriendly,
                    business.IsKidFriendly,
                    business.IsNearPublicTransport,
                    business.UsesOrganicProducts,
                    business.UsesVeganProducts,
                    business.IsEnvironmentallyFriendly)))
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
                service.Description,
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
            .Join(
                dbContext.Users.AsNoTracking(),
                staffMember => staffMember.UserId,
                user => user.Id,
                (staffMember, user) => new
                {
                    staffMember.Id,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    user.ProfilePhotoId
                })
            .OrderBy(row => row.FirstName)
            .ThenBy(row => row.LastName)
            .ToListAsync(cancellationToken);
        var staffMemberResponses = staffMembers
            .Select(staffMember => new PublicBusinessStaffMemberResponse(
                staffMember.Id,
                UserNames.FormatFullName(staffMember.FirstName, staffMember.LastName),
                ProfilePhotoUrls.Build(staffMember.ProfilePhotoId)))
            .ToList();

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

        var reviewRows = await (
                from review in dbContext.BusinessReviews.AsNoTracking()
                where review.BusinessId == id && review.IsPublic
                join user in dbContext.Users.AsNoTracking()
                    on review.CustomerUserId equals user.Id into userRows
                from user in userRows.DefaultIfEmpty()
                orderby review.CreatedAtUtc descending
                select new
                {
                    review.Id,
                    review.CustomerName,
                    review.CustomerInitial,
                    review.Rating,
                    review.Comment,
                    review.CreatedAtUtc,
                    CustomerProfilePhotoId = user == null ? null : user.ProfilePhotoId
                })
            .ToListAsync(cancellationToken);
        var reviews = reviewRows
            .Select(review => new PublicBusinessReviewResponse(
                review.Id,
                review.CustomerName,
                review.CustomerInitial,
                review.Rating,
                review.Comment,
                review.CreatedAtUtc,
                ProfilePhotoUrls.Build(review.CustomerProfilePhotoId)))
            .ToList();

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
            staffMemberResponses,
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
    PublicBusinessAddressResponse Address,
    IReadOnlyList<PublicBusinessSummaryServiceResponse> Services,
    IReadOnlyList<PublicBusinessWorkingHourResponse> WorkingHours,
    IReadOnlyList<PublicBusinessPhotoResponse> Photos,
    PublicBusinessReviewSummaryResponse ReviewSummary,
    IReadOnlyList<string> AdditionalInformation);

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
    string Description,
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
    string DisplayName,
    string? ProfilePhotoUrl);

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
    DateTimeOffset CreatedAtUtc,
    string? CustomerProfilePhotoUrl);
