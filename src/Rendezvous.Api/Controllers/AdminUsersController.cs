using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RoleManager<IdentityRole<Guid>> roleManager;

    public AdminUsersController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.roleManager = roleManager;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserSummaryResponse>>> List(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(user =>
                (user.Email != null && user.Email.ToLower().Contains(normalizedSearch))
                || user.PublicNumber.ToString().Contains(normalizedSearch)
                || (user.FirstName != null && user.FirstName.ToLower().Contains(normalizedSearch))
                || (user.LastName != null && user.LastName.ToLower().Contains(normalizedSearch))
                || ((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty))
                    .ToLower()
                    .Contains(normalizedSearch));
        }

        var users = await query
            .OrderBy(user => user.Email)
            .Select(user => new
            {
                user.Id,
                user.PublicNumber,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                user.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToList();
        var roles = await GetRolesAsync(userIds, cancellationToken);

        return users
            .Select(user => new AdminUserSummaryResponse(
                user.Id,
                user.PublicNumber,
                user.Email,
                user.FirstName,
                user.LastName,
                UserNames.FormatFullName(user.FirstName, user.LastName),
                IsSuspended(user.LockoutEnd),
                roles.GetValueOrDefault(user.Id, [])))
            .ToList();
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailResponse>> Get(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.PublicNumber,
                Email = candidate.Email ?? string.Empty,
                FirstName = candidate.FirstName ?? string.Empty,
                LastName = candidate.LastName ?? string.Empty,
                candidate.LockoutEnd
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await GetRolesAsync([user.Id], cancellationToken);

        var membershipRows = await dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == user.Id)
            .Join(
                dbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
                (membership, business) => new
                {
                    BusinessId = business.Id,
                    BusinessName = business.Name,
                    membership.Role,
                    membership.Status
                })
            .OrderBy(membership => membership.BusinessName)
            .ToListAsync(cancellationToken);

        var memberships = membershipRows
            .Select(membership => new AdminUserBusinessMembershipResponse(
                membership.BusinessId,
                membership.BusinessName,
                membership.Role.ToString(),
                membership.Status.ToString()))
            .ToList();

        return new AdminUserDetailResponse(
            user.Id,
            user.PublicNumber,
            user.Email,
            user.FirstName,
            user.LastName,
            UserNames.FormatFullName(user.FirstName, user.LastName),
            IsSuspended(user.LockoutEnd),
            roles.GetValueOrDefault(user.Id, []),
            memberships);
    }

    [HttpPost("{userId:guid}/suspend")]
    public async Task<ActionResult<AdminUserDetailResponse>> Suspend(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        await userManager.UpdateAsync(user);

        return await Get(userId, cancellationToken);
    }

    [HttpPost("{userId:guid}/unsuspend")]
    public async Task<ActionResult<AdminUserDetailResponse>> Unsuspend(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        user.LockoutEnd = null;
        await userManager.UpdateAsync(user);

        return await Get(userId, cancellationToken);
    }

    [HttpPost("{userId:guid}/roles")]
    public async Task<ActionResult<AdminUserDetailResponse>> AddRole(
        Guid userId,
        AdminUserRoleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var roleName = request.RoleName.Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return BadRequest(new { message = "Role name is required." });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }

        return await Get(userId, cancellationToken);
    }

    [HttpDelete("{userId:guid}/roles/{roleName}")]
    public async Task<ActionResult<AdminUserDetailResponse>> RemoveRole(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.RemoveFromRoleAsync(user, roleName);
        }

        return await Get(userId, cancellationToken);
    }

    [HttpPost("{userId:guid}/business-memberships")]
    public async Task<ActionResult<AdminUserDetailResponse>> UpsertBusinessMembership(
        Guid userId,
        AdminBusinessMembershipMutationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.FirstName,
                candidate.LastName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var businessExists = await dbContext.Businesses
            .AsNoTracking()
            .AnyAsync(business => business.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
        {
            return BadRequest(new { message = "Business was not found." });
        }

        var membership = await dbContext.BusinessMemberships
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.UserId == userId
                    && candidate.BusinessId == request.BusinessId,
                cancellationToken);

        if (membership is null)
        {
            dbContext.BusinessMemberships.Add(new BusinessMembership
            {
                UserId = userId,
                BusinessId = request.BusinessId,
                Role = request.Role,
                Status = request.Status,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            membership.Role = request.Role;
            membership.Status = request.Status;
        }

        if (request.Role == BusinessMembershipRole.Employee)
        {
            var hasStaffProfile = await dbContext.StaffMembers
                .AnyAsync(
                    staffMember =>
                        staffMember.BusinessId == request.BusinessId
                        && staffMember.UserId == userId,
                    cancellationToken);

            if (!hasStaffProfile)
            {
                dbContext.StaffMembers.Add(new StaffMember
                {
                    BusinessId = request.BusinessId,
                    UserId = userId,
                    IsActive = request.Status == BusinessMembershipStatus.Active
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await Get(userId, cancellationToken);
    }

    [HttpPost("{userId:guid}/business-memberships/{businessId:guid}/suspend")]
    public Task<ActionResult<AdminUserDetailResponse>> SuspendMembership(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return ChangeMembershipStatusAsync(userId, businessId, BusinessMembershipStatus.Suspended, cancellationToken);
    }

    [HttpPost("{userId:guid}/business-memberships/{businessId:guid}/activate")]
    public Task<ActionResult<AdminUserDetailResponse>> ActivateMembership(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return ChangeMembershipStatusAsync(userId, businessId, BusinessMembershipStatus.Active, cancellationToken);
    }

    private async Task<ActionResult<AdminUserDetailResponse>> ChangeMembershipStatusAsync(
        Guid userId,
        Guid businessId,
        BusinessMembershipStatus status,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.BusinessMemberships
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.UserId == userId
                    && candidate.BusinessId == businessId,
                cancellationToken);

        if (membership is null)
        {
            return NotFound();
        }

        membership.Status = status;

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.UserId == userId
                    && candidate.BusinessId == businessId,
                cancellationToken);

        if (staffMember is not null)
        {
            staffMember.IsActive = status == BusinessMembershipStatus.Active;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await Get(userId, cancellationToken);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetRolesAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var roleRows = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new
                {
                    userRole.UserId,
                    RoleName = role.Name ?? string.Empty
                })
            .ToListAsync(cancellationToken);

        return roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.RoleName)
                    .OrderBy(role => role)
                    .ToList());
    }

    private static bool IsSuspended(DateTimeOffset? lockoutEnd)
    {
        return lockoutEnd is not null && lockoutEnd > DateTimeOffset.UtcNow;
    }
}

public sealed record AdminUserSummaryResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    bool IsSuspended,
    IReadOnlyList<string> Roles);

public sealed record AdminUserDetailResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    bool IsSuspended,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminUserBusinessMembershipResponse> BusinessMemberships);

public sealed record AdminUserBusinessMembershipResponse(
    Guid BusinessId,
    string BusinessName,
    string Role,
    string Status);

public sealed record AdminUserRoleMutationRequest(string RoleName);

public sealed record AdminBusinessMembershipMutationRequest(
    Guid BusinessId,
    BusinessMembershipRole Role,
    BusinessMembershipStatus Status);
