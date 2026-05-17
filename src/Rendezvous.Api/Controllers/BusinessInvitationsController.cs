using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/business-invitations")]
public class BusinessInvitationsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly InvitationTokenService invitationTokenService;

    public BusinessInvitationsController(
        AppDbContext dbContext,
        InvitationTokenService invitationTokenService)
    {
        this.dbContext = dbContext;
        this.invitationTokenService = invitationTokenService;
    }

    [HttpPost("accept")]
    public async Task<ActionResult<AcceptedBusinessInvitationResponse>> Accept(
        AcceptBusinessInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Invitation token is required." });
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Unauthorized();
        }

        var tokenHash = invitationTokenService.HashToken(request.Token.Trim());
        var invitation = await dbContext.BusinessInvitations
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            return NotFound();
        }

        if (invitation.Status != BusinessInvitationStatus.Pending
            || invitation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            if (invitation.Status == BusinessInvitationStatus.Pending)
            {
                invitation.Status = BusinessInvitationStatus.Expired;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return BadRequest(new { message = "This invitation is not active." });
        }

        if (!string.Equals(invitation.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var existingMembership = await dbContext.BusinessMemberships
            .SingleOrDefaultAsync(
                membership => membership.BusinessId == invitation.BusinessId && membership.UserId == user.Id,
                cancellationToken);

        if (existingMembership is not null && existingMembership.Role != BusinessMembershipRole.Employee)
        {
            return Conflict(new { message = "This user already has a different business role." });
        }

        if (existingMembership is null)
        {
            dbContext.BusinessMemberships.Add(new BusinessMembership
            {
                BusinessId = invitation.BusinessId,
                UserId = user.Id,
                Role = BusinessMembershipRole.Employee,
                Status = BusinessMembershipStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existingMembership.Status = BusinessMembershipStatus.Active;
        }

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == invitation.BusinessId && candidate.UserId == user.Id,
                cancellationToken);

        if (staffMember is null)
        {
            dbContext.StaffMembers.Add(new StaffMember
            {
                BusinessId = invitation.BusinessId,
                UserId = user.Id,
                IsActive = true
            });
        }
        else
        {
            staffMember.IsActive = true;
        }

        invitation.Status = BusinessInvitationStatus.Accepted;
        invitation.AcceptedByUserId = user.Id;
        invitation.AcceptedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var business = await dbContext.Businesses
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == invitation.BusinessId, cancellationToken);

        return new AcceptedBusinessInvitationResponse(
            business.Id,
            business.Name,
            BusinessMembershipRole.Employee.ToString());
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record AcceptBusinessInvitationRequest(string Token);

public sealed record AcceptedBusinessInvitationResponse(
    Guid BusinessId,
    string BusinessName,
    string Role);
