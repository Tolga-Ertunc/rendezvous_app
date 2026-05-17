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
[Route("api/owner/businesses/{businessId:guid}/invitations")]
public class OwnerBusinessInvitationsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly InvitationTokenService invitationTokenService;

    public OwnerBusinessInvitationsController(
        AppDbContext dbContext,
        InvitationTokenService invitationTokenService)
    {
        this.dbContext = dbContext;
        this.invitationTokenService = invitationTokenService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerBusinessInvitationResponse>>> List(
        Guid businessId,
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

        await ExpireOldInvitationsAsync(businessId, cancellationToken);

        return await dbContext.BusinessInvitations
            .AsNoTracking()
            .Where(invitation => invitation.BusinessId == businessId)
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .Select(invitation => new OwnerBusinessInvitationResponse(
                invitation.Id,
                invitation.Email,
                invitation.Role.ToString(),
                invitation.Status.ToString(),
                invitation.CreatedAtUtc,
                invitation.ExpiresAtUtc,
                invitation.AcceptedAtUtc,
                null))
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<OwnerBusinessInvitationResponse>> Create(
        Guid businessId,
        CreateBusinessInvitationRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingPendingInvitation = await dbContext.BusinessInvitations
            .AnyAsync(
                invitation =>
                    invitation.BusinessId == businessId
                    && invitation.Email.ToLower() == normalizedEmail
                    && invitation.Status == BusinessInvitationStatus.Pending
                    && invitation.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        if (existingPendingInvitation)
        {
            return Conflict(new { message = "This email already has a pending invitation." });
        }

        var token = invitationTokenService.CreateToken();
        var invitation = new BusinessInvitation
        {
            BusinessId = businessId,
            CreatedByUserId = userId.Value,
            Email = normalizedEmail,
            TokenHash = invitationTokenService.HashToken(token),
            Role = BusinessMembershipRole.Employee,
            Status = BusinessInvitationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };

        dbContext.BusinessInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/owner/businesses/{businessId}/invitations/{invitation.Id}",
            new OwnerBusinessInvitationResponse(
                invitation.Id,
                invitation.Email,
                invitation.Role.ToString(),
                invitation.Status.ToString(),
                invitation.CreatedAtUtc,
                invitation.ExpiresAtUtc,
                invitation.AcceptedAtUtc,
                token));
    }

    private async Task ExpireOldInvitationsAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var invitations = await dbContext.BusinessInvitations
            .Where(invitation =>
                invitation.BusinessId == businessId
                && invitation.Status == BusinessInvitationStatus.Pending
                && invitation.ExpiresAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var invitation in invitations)
        {
            invitation.Status = BusinessInvitationStatus.Expired;
        }

        if (invitations.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record CreateBusinessInvitationRequest(string Email);

public sealed record OwnerBusinessInvitationResponse(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    string? AcceptanceToken);
