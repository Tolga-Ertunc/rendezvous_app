using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner-onboarding-requests")]
public class OwnerOnboardingController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly BusinessProvisioningService businessProvisioningService;
    private readonly NotificationWriter notificationWriter;

    public OwnerOnboardingController(
        AppDbContext dbContext,
        BusinessProvisioningService businessProvisioningService,
        NotificationWriter notificationWriter)
    {
        this.dbContext = dbContext;
        this.businessProvisioningService = businessProvisioningService;
        this.notificationWriter = notificationWriter;
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<OwnerOnboardingRequestResponse>>> MyRequests(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return await dbContext.OwnerOnboardingRequests
            .AsNoTracking()
            .Where(request => request.RequesterUserId == userId.Value)
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(request => new OwnerOnboardingRequestResponse(
                request.Id,
                request.RequesterUserId,
                request.BusinessName,
                request.BusinessType.ToString(),
                request.OwnerStaffDisplayName,
                request.Status.ToString(),
                request.AdminNote,
                request.CreatedBusinessId,
                request.CreatedAtUtc,
                request.ReviewedAtUtc))
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<OwnerOnboardingRequestResponse>> Create(
        CreateOwnerOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return BadRequest(new { message = "Business name is required." });
        }

        var hasPendingRequest = await dbContext.OwnerOnboardingRequests
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.RequesterUserId == userId.Value
                    && candidate.Status == OwnerOnboardingRequestStatus.Pending,
                cancellationToken);

        if (hasPendingRequest)
        {
            return Conflict(new { message = "This account already has a pending owner application." });
        }

        var ownerRequest = new OwnerOnboardingRequest
        {
            RequesterUserId = userId.Value,
            BusinessName = request.BusinessName.Trim(),
            BusinessType = request.BusinessType,
            OwnerStaffDisplayName = string.IsNullOrWhiteSpace(request.OwnerStaffDisplayName)
                ? request.BusinessName.Trim()
                : request.OwnerStaffDisplayName.Trim(),
            Status = OwnerOnboardingRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.OwnerOnboardingRequests.Add(ownerRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/owner-onboarding-requests/{ownerRequest.Id}",
            Map(ownerRequest));
    }

    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpGet("/api/admin/owner-onboarding-requests")]
    public async Task<ActionResult<IReadOnlyList<AdminOwnerOnboardingRequestResponse>>> AdminList(
        [FromQuery] OwnerOnboardingRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.OwnerOnboardingRequests.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(request => request.Status == status);
        }

        return await query
            .Join(
                dbContext.Users.AsNoTracking(),
                request => request.RequesterUserId,
                user => user.Id,
                (request, user) => new { request, user })
            .OrderByDescending(row => row.request.CreatedAtUtc)
            .Select(row => new AdminOwnerOnboardingRequestResponse(
                row.request.Id,
                row.request.RequesterUserId,
                row.user.Email ?? string.Empty,
                row.user.PublicNumber,
                row.request.BusinessName,
                row.request.BusinessType.ToString(),
                row.request.OwnerStaffDisplayName,
                row.request.Status.ToString(),
                row.request.AdminNote,
                row.request.CreatedBusinessId,
                row.request.CreatedAtUtc,
                row.request.ReviewedAtUtc))
            .ToListAsync(cancellationToken);
    }

    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpPost("/api/admin/owner-onboarding-requests/{requestId:guid}/approve")]
    public async Task<ActionResult<OwnerOnboardingRequestResponse>> Approve(
        Guid requestId,
        ReviewOwnerOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        if (adminUserId is null)
        {
            return Unauthorized();
        }

        var ownerRequest = await dbContext.OwnerOnboardingRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);

        if (ownerRequest is null)
        {
            return NotFound();
        }

        if (ownerRequest.Status != OwnerOnboardingRequestStatus.Pending)
        {
            return BadRequest(new { message = "Only pending owner applications can be approved." });
        }

        var business = businessProvisioningService.CreateOwnedBusiness(
            ownerRequest.RequesterUserId,
            ownerRequest.BusinessName,
            ownerRequest.BusinessType,
            ownerRequest.OwnerStaffDisplayName,
            BusinessStatus.Approved);

        ownerRequest.Status = OwnerOnboardingRequestStatus.Approved;
        ownerRequest.AdminNote = request.AdminNote;
        ownerRequest.CreatedBusinessId = business.Id;
        ownerRequest.ReviewedByUserId = adminUserId.Value;
        ownerRequest.ReviewedAtUtc = DateTime.UtcNow;

        notificationWriter.Add(
            ownerRequest.RequesterUserId,
            "Owner application approved",
            $"{ownerRequest.BusinessName} is ready to manage.",
            NotificationType.OwnerOnboardingApproved,
            $"/owner/businesses/{business.Id}/overview");

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(ownerRequest);
    }

    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpPost("/api/admin/owner-onboarding-requests/{requestId:guid}/reject")]
    public async Task<ActionResult<OwnerOnboardingRequestResponse>> Reject(
        Guid requestId,
        ReviewOwnerOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        if (adminUserId is null)
        {
            return Unauthorized();
        }

        var ownerRequest = await dbContext.OwnerOnboardingRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);

        if (ownerRequest is null)
        {
            return NotFound();
        }

        if (ownerRequest.Status != OwnerOnboardingRequestStatus.Pending)
        {
            return BadRequest(new { message = "Only pending owner applications can be rejected." });
        }

        ownerRequest.Status = OwnerOnboardingRequestStatus.Rejected;
        ownerRequest.AdminNote = request.AdminNote;
        ownerRequest.ReviewedByUserId = adminUserId.Value;
        ownerRequest.ReviewedAtUtc = DateTime.UtcNow;

        notificationWriter.Add(
            ownerRequest.RequesterUserId,
            "Owner application rejected",
            $"{ownerRequest.BusinessName} was not approved.",
            NotificationType.OwnerOnboardingRejected,
            "/profile/owner-onboarding");

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(ownerRequest);
    }

    private static OwnerOnboardingRequestResponse Map(OwnerOnboardingRequest request)
    {
        return new OwnerOnboardingRequestResponse(
            request.Id,
            request.RequesterUserId,
            request.BusinessName,
            request.BusinessType.ToString(),
            request.OwnerStaffDisplayName,
            request.Status.ToString(),
            request.AdminNote,
            request.CreatedBusinessId,
            request.CreatedAtUtc,
            request.ReviewedAtUtc);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record CreateOwnerOnboardingRequest(
    string BusinessName,
    BusinessType BusinessType,
    string? OwnerStaffDisplayName);

public sealed record ReviewOwnerOnboardingRequest(string? AdminNote);

public sealed record OwnerOnboardingRequestResponse(
    Guid Id,
    Guid RequesterUserId,
    string BusinessName,
    string BusinessType,
    string OwnerStaffDisplayName,
    string Status,
    string? AdminNote,
    Guid? CreatedBusinessId,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);

public sealed record AdminOwnerOnboardingRequestResponse(
    Guid Id,
    Guid RequesterUserId,
    string RequesterEmail,
    int RequesterPublicNumber,
    string BusinessName,
    string BusinessType,
    string OwnerStaffDisplayName,
    string Status,
    string? AdminNote,
    Guid? CreatedBusinessId,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
