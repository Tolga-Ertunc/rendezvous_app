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
                candidate.TimeZoneId
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
                service.DurationMinutes,
                service.BasePriceAmount,
                service.CurrencyCode))
            .ToListAsync(cancellationToken);

        return new PublicBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.TimeZoneId,
            services);
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
    IReadOnlyList<PublicBusinessServiceResponse> Services);

public sealed record PublicBusinessServiceResponse(
    Guid Id,
    string Name,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode);
