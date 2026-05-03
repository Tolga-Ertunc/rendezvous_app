using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public AdminUsersController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
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
                || user.PublicNumber.ToString().Contains(normalizedSearch));
        }

        var users = await query
            .OrderBy(user => user.Email)
            .Select(user => new
            {
                user.Id,
                user.PublicNumber,
                Email = user.Email ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToList();
        var roles = await GetRolesAsync(userIds, cancellationToken);

        return users
            .Select(user => new AdminUserSummaryResponse(
                user.Id,
                user.PublicNumber,
                user.Email,
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
                Email = candidate.Email ?? string.Empty
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await GetRolesAsync([user.Id], cancellationToken);

        var memberships = await dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == user.Id)
            .Join(
                dbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
                (membership, business) => new AdminUserBusinessMembershipResponse(
                    business.Id,
                    business.Name,
                    membership.Role.ToString(),
                    membership.Status.ToString()))
            .OrderBy(membership => membership.BusinessName)
            .ToListAsync(cancellationToken);

        return new AdminUserDetailResponse(
            user.Id,
            user.PublicNumber,
            user.Email,
            roles.GetValueOrDefault(user.Id, []),
            memberships);
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
}

public sealed record AdminUserSummaryResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record AdminUserDetailResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminUserBusinessMembershipResponse> BusinessMemberships);

public sealed record AdminUserBusinessMembershipResponse(
    Guid BusinessId,
    string BusinessName,
    string Role,
    string Status);
