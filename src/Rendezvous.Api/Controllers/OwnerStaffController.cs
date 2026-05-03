using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Staff;
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

    [HttpPut("{staffMemberId:guid}")]
    public async Task<ActionResult<OwnerStaffMutationResponse>> Update(
        Guid businessId,
        Guid staffMemberId,
        OwnerStaffRequest request,
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

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Staff display name is required." });
        }

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate => candidate.Id == staffMemberId && candidate.BusinessId == businessId,
                cancellationToken);

        if (staffMember is null)
        {
            return NotFound();
        }

        staffMember.DisplayName = request.DisplayName.Trim();
        staffMember.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(staffMember);
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

        return Map(staffMember);
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

    private static OwnerStaffMutationResponse Map(StaffMember staffMember)
    {
        return new OwnerStaffMutationResponse(
            staffMember.Id,
            staffMember.DisplayName,
            staffMember.IsActive);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record OwnerStaffRequest(
    string DisplayName,
    bool IsActive);

public sealed record OwnerStaffMutationResponse(
    Guid Id,
    string DisplayName,
    bool IsActive);
