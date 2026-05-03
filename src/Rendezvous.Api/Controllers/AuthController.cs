using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rendezvous.Api.Authentication;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly AuthTokenService tokenService;
    private readonly JwtOptions jwtOptions;
    private readonly PublicNumberGenerator publicNumberGenerator;

    public AuthController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        AuthTokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        PublicNumberGenerator publicNumberGenerator)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.tokenService = tokenService;
        this.jwtOptions = jwtOptions.Value;
        this.publicNumberGenerator = publicNumberGenerator;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized();
        }

        return await CreateTokenResponseAsync(user, cancellationToken);
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthTokenResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { message = "Passwords do not match." });
        }

        var email = request.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new ApplicationUser
        {
            PublicNumber = await publicNumberGenerator.GenerateAsync(cancellationToken),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Registration failed.",
                errors = result.Errors.Select(error => error.Description).ToList()
            });
        }

        var roleResult = await userManager.AddToRoleAsync(user, ApplicationRoles.User);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Registration role assignment failed." });
        }

        return await CreateTokenResponseAsync(user, cancellationToken);
    }

    [HttpGet("email-availability")]
    public async Task<ActionResult<EmailAvailabilityResponse>> EmailAvailability([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var normalizedEmail = email.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);

        return new EmailAvailabilityResponse(
            normalizedEmail,
            existingUser is null);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Unauthorized();
        }

        var nowUtc = DateTime.UtcNow;
        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var storedRefreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.TokenHash == refreshTokenHash,
                cancellationToken);

        if (storedRefreshToken is null
            || storedRefreshToken.RevokedAtUtc is not null
            || storedRefreshToken.ExpiresAtUtc <= nowUtc)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(storedRefreshToken.UserId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var newRefreshToken = tokenService.CreateRefreshToken();

        return await CreateTokenResponseAsync(
            user,
            newRefreshToken,
            storedRefreshToken,
            cancellationToken);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return NoContent();
        }

        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var storedRefreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.TokenHash == refreshTokenHash,
                cancellationToken);

        if (storedRefreshToken is not null && storedRefreshToken.RevokedAtUtc is null)
        {
            storedRefreshToken.RevokedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var membershipRows = await dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == user.Id
                && membership.Status == BusinessMembershipStatus.Active)
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
            .Select(membership => new CurrentUserBusinessMembershipResponse(
                membership.BusinessId,
                membership.BusinessName,
                membership.Role.ToString(),
                membership.Status.ToString()))
            .ToList();

        return new CurrentUserResponse(
            user.Id,
            user.PublicNumber,
            user.Email ?? string.Empty,
            roles.OrderBy(role => role).ToList(),
            memberships);
    }

    private async Task<AuthTokenResponse> CreateTokenResponseAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var refreshToken = tokenService.CreateRefreshToken();

        return await CreateTokenResponseAsync(user, refreshToken, null, cancellationToken);
    }

    private async Task<AuthTokenResponse> CreateTokenResponseAsync(
        ApplicationUser user,
        string refreshToken,
        RefreshToken? refreshTokenToRevoke,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.CreateAccessTokenAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var nowUtc = DateTime.UtcNow;
        var storedRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(refreshToken),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddDays(jwtOptions.RefreshTokenDays)
        };

        if (refreshTokenToRevoke is not null)
        {
            refreshTokenToRevoke.RevokedAtUtc = nowUtc;
            refreshTokenToRevoke.ReplacedByTokenId = storedRefreshToken.Id;
        }

        dbContext.RefreshTokens.Add(storedRefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var userResponse = new AuthenticatedUserResponse(
            user.Id,
            user.PublicNumber,
            user.Email ?? string.Empty,
            roles.OrderBy(role => role).ToList());

        return new AuthTokenResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken,
            userResponse);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword);

public sealed record EmailAvailabilityResponse(
    string Email,
    bool IsAvailable);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    AuthenticatedUserResponse User);

public sealed record AuthenticatedUserResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record CurrentUserResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<CurrentUserBusinessMembershipResponse> BusinessMemberships);

public sealed record CurrentUserBusinessMembershipResponse(
    Guid BusinessId,
    string BusinessName,
    string Role,
    string Status);
