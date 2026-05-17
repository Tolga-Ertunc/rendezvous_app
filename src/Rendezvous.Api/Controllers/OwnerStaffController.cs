using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner/businesses/{businessId:guid}/staff")]
public class OwnerStaffController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public OwnerStaffController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpPost("{staffMemberId:guid}/activate")]
    public Task<ActionResult<OwnerStaffMutationResponse>> Activate(
        Guid businessId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        return ChangeActiveStateAsync(businessId, staffMemberId, true, cancellationToken);
    }

    [HttpPost("{staffMemberId:guid}/deactivate")]
    public Task<ActionResult<OwnerStaffMutationResponse>> Deactivate(
        Guid businessId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        return ChangeActiveStateAsync(businessId, staffMemberId, false, cancellationToken);
    }

    private async Task<ActionResult<OwnerStaffMutationResponse>> ChangeActiveStateAsync(
        Guid businessId,
        Guid staffMemberId,
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

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate => candidate.Id == staffMemberId && candidate.BusinessId == businessId,
                cancellationToken);

        if (staffMember is null)
        {
            return NotFound();
        }

        staffMember.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await MapAsync(staffMember.Id, cancellationToken);
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

    private async Task<OwnerStaffMutationResponse> MapAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staffMember => staffMember.Id == staffMemberId)
            .Join(
                dbContext.Users.AsNoTracking(),
                staffMember => staffMember.UserId,
                user => user.Id,
                (staffMember, user) => new
                {
                    staffMember.Id,
                    staffMember.IsActive,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty
                })
            .SingleAsync(cancellationToken);

        return new OwnerStaffMutationResponse(
            row.Id,
            UserNames.FormatFullName(row.FirstName, row.LastName),
            row.Email,
            row.IsActive);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record OwnerStaffMutationResponse(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsActive);
