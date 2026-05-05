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

        var business = businessProvisioningService.CreateOwnedBusiness(
            userId.Value,
            request.Name,
            request.Type,
            request.OwnerStaffDisplayName ?? string.Empty,
            BusinessStatus.PendingApproval);

        await dbContext.SaveChangesAsync(cancellationToken);

        var staffMember = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(candidate => candidate.BusinessId == business.Id && candidate.UserId == userId.Value)
            .SingleAsync(cancellationToken);

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
