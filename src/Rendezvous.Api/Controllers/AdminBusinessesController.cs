using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/admin/businesses")]
public class AdminBusinessesController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public AdminBusinessesController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBusinessSummaryResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] BusinessStatus? status,
        [FromQuery] BusinessType? type,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Businesses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(business => business.Name.ToLower().Contains(normalizedSearch));
        }

        if (status is not null)
        {
            query = query.Where(business => business.Status == status);
        }

        if (type is not null)
        {
            query = query.Where(business => business.Type == type);
        }

        var businessRows = await query
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
            .Select(business => new AdminBusinessSummaryResponse(
                business.Id,
                business.Name,
                business.Type.ToString(),
                business.Status.ToString(),
                business.TimeZoneId))
            .ToList();
    }

    [HttpGet("{businessId:guid}")]
    public async Task<ActionResult<AdminBusinessDetailResponse>> Get(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .AsNoTracking()
            .Where(candidate => candidate.Id == businessId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Type,
                candidate.Status,
                candidate.TimeZoneId,
                candidate.OwnerUserId
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
            .Select(service => new AdminBusinessServiceResponse(
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
            .Select(staffMember => new AdminBusinessStaffMemberResponse(
                staffMember.Id,
                staffMember.DisplayName,
                staffMember.IsActive))
            .ToListAsync(cancellationToken);

        var owner = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == business.OwnerUserId)
            .Select(user => new
            {
                user.Id,
                user.PublicNumber,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty
            })
            .SingleOrDefaultAsync(cancellationToken);
        var ownerResponse = owner is null
            ? null
            : new AdminBusinessOwnerResponse(
                owner.Id,
                owner.PublicNumber,
                owner.Email,
                owner.FirstName,
                owner.LastName,
                UserNames.FormatFullName(owner.FirstName, owner.LastName));

        var appointmentCount = await dbContext.Appointments
            .AsNoTracking()
            .CountAsync(appointment => appointment.BusinessId == businessId, cancellationToken);

        return new AdminBusinessDetailResponse(
            business.Id,
            business.Name,
            business.Type.ToString(),
            business.Status.ToString(),
            business.TimeZoneId,
            ownerResponse,
            services.Count,
            staffMembers.Count,
            appointmentCount,
            services,
            staffMembers);
    }

    [HttpPost("{businessId:guid}/approve")]
    public Task<ActionResult<AdminBusinessStatusResponse>> Approve(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return ChangeStatusAsync(businessId, BusinessStatus.Approved, cancellationToken);
    }

    [HttpPost("{businessId:guid}/suspend")]
    public Task<ActionResult<AdminBusinessStatusResponse>> Suspend(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return ChangeStatusAsync(businessId, BusinessStatus.Suspended, cancellationToken);
    }

    [HttpPost("{businessId:guid}/reject")]
    public Task<ActionResult<AdminBusinessStatusResponse>> Reject(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return ChangeStatusAsync(businessId, BusinessStatus.Rejected, cancellationToken);
    }

    private async Task<ActionResult<AdminBusinessStatusResponse>> ChangeStatusAsync(
        Guid businessId,
        BusinessStatus status,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .SingleOrDefaultAsync(candidate => candidate.Id == businessId, cancellationToken);

        if (business is null)
        {
            return NotFound();
        }

        business.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminBusinessStatusResponse(
            business.Id,
            business.Status.ToString());
    }
}

public sealed record AdminBusinessSummaryResponse(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string TimeZoneId);

public sealed record AdminBusinessDetailResponse(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string TimeZoneId,
    AdminBusinessOwnerResponse? Owner,
    int ServiceCount,
    int StaffCount,
    int AppointmentCount,
    IReadOnlyList<AdminBusinessServiceResponse> Services,
    IReadOnlyList<AdminBusinessStaffMemberResponse> StaffMembers);

public sealed record AdminBusinessOwnerResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    string FirstName,
    string LastName,
    string FullName);

public sealed record AdminBusinessServiceResponse(
    Guid Id,
    string Name,
    string CategoryName,
    string Description,
    int DurationMinutes,
    decimal BasePriceAmount,
    string CurrencyCode,
    bool IsActive);

public sealed record AdminBusinessStaffMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive);

public sealed record AdminBusinessStatusResponse(
    Guid Id,
    string Status);
