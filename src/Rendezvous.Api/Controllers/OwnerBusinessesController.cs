using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner/businesses")]
public class OwnerBusinessesController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public OwnerBusinessesController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
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

        var business = new Business
        {
            OwnerUserId = userId.Value,
            Name = request.Name.Trim(),
            Type = request.Type,
            Status = BusinessStatus.PendingApproval,
            TimeZoneId = "Europe/Istanbul"
        };

        var membership = new BusinessMembership
        {
            BusinessId = business.Id,
            UserId = userId.Value,
            Role = BusinessMembershipRole.Owner,
            Status = BusinessMembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        var staffMember = new StaffMember
        {
            BusinessId = business.Id,
            UserId = userId.Value,
            DisplayName = string.IsNullOrWhiteSpace(request.OwnerStaffDisplayName)
                ? request.Name.Trim()
                : request.OwnerStaffDisplayName.Trim(),
            IsActive = true
        };

        dbContext.Businesses.Add(business);
        dbContext.BusinessMemberships.Add(membership);
        dbContext.StaffMembers.Add(staffMember);
        AddDefaultBusinessWorkingHours(business.Id);
        AddDefaultStaffWorkingHours(staffMember.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OwnerBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.Status.ToString(),
            business.TimeZoneId,
            [],
            [
                new OwnerBusinessStaffMemberResponse(
                    staffMember.Id,
                    staffMember.DisplayName,
                    staffMember.IsActive)
            ]);

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
                candidate.TimeZoneId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return NotFound();
        }

        var services = await dbContext.BusinessServices
            .AsNoTracking()
            .Where(service => service.BusinessId == businessId)
            .OrderBy(service => service.Name)
            .Select(service => new OwnerBusinessServiceResponse(
                service.Id,
                service.Name,
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

        return new OwnerBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.Status.ToString(),
            business.TimeZoneId,
            services,
            staffMembers);
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

    private void AddDefaultBusinessWorkingHours(Guid businessId)
    {
        foreach (var day in Enumerable.Range(1, 6))
        {
            dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
            {
                BusinessId = businessId,
                DayOfWeek = (DayOfWeek)day,
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(18, 0)
            });
        }
    }

    private void AddDefaultStaffWorkingHours(Guid staffMemberId)
    {
        foreach (var day in Enumerable.Range(1, 6))
        {
            dbContext.StaffWorkingHours.Add(new StaffWorkingHour
            {
                StaffMemberId = staffMemberId,
                DayOfWeek = (DayOfWeek)day,
                StartsAt = new TimeOnly(9, 0),
                EndsAt = new TimeOnly(18, 0)
            });
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
    IReadOnlyList<OwnerBusinessServiceResponse> Services,
    IReadOnlyList<OwnerBusinessStaffMemberResponse> StaffMembers);

public sealed record OwnerBusinessServiceResponse(
    Guid Id,
    string Name,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode,
    bool IsActive);

public sealed record OwnerBusinessStaffMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive);
