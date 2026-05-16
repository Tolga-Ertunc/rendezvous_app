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
    private readonly EmailConfirmationService emailConfirmationService;

    public AuthController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        AuthTokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        EmailConfirmationService emailConfirmationService)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.tokenService = tokenService;
        this.jwtOptions = jwtOptions.Value;
        this.emailConfirmationService = emailConfirmationService;
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
        if (user is null
            || IsBlockedForAuthentication(user)
            || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized();
        }

        return await CreateTokenResponseAsync(user, cancellationToken);
    }

    [HttpPost("register")]
    public async Task<ActionResult<PendingEmailRegistrationResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.LastName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(new { message = "First name, last name, email, and password are required." });
        }

        if (!UserNames.IsValidNamePart(request.FirstName)
            || !UserNames.IsValidNamePart(request.LastName))
        {
            return BadRequest(new
            {
                message = "First name and last name must be 1 to 100 characters and use valid name characters."
            });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { message = "Passwords do not match." });
        }

        var email = request.Email.Trim();
        var firstName = UserNames.Normalize(request.FirstName);
        var lastName = UserNames.Normalize(request.LastName);

        try
        {
            var result = await emailConfirmationService.StartAsync(
                email,
                firstName,
                lastName,
                request.Password,
                cancellationToken);

            return Accepted(ToPendingEmailRegistrationResponse(result));
        }
        catch (DuplicateEmailException)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }
        catch (InvalidRegistrationException exception)
        {
            return BadRequest(new
            {
                message = "Registration failed.",
                errors = exception.Errors.Select(error => error.Description).ToList()
            });
        }
        catch (ConfirmationCodeCooldownException exception)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = "Please wait before requesting a new confirmation code.", exception.ResendAvailableAtUtc });
        }
    }

    [HttpGet("email-availability")]
    public async Task<ActionResult<EmailAvailabilityResponse>> EmailAvailability(
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var trimmedEmail = email.Trim();
        var normalizedEmail = userManager.NormalizeEmail(trimmedEmail);
        var existingUser = await userManager.FindByEmailAsync(trimmedEmail);
        var existingPendingRegistration = await dbContext.PendingEmailRegistrations
            .AsNoTracking()
            .AnyAsync(
                registration =>
                    registration.NormalizedEmail == normalizedEmail
                    && registration.CodeExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        return new EmailAvailabilityResponse(
            trimmedEmail,
            existingUser is null && !existingPendingRegistration);
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Email and confirmation code are required." });
        }

        try
        {
            await emailConfirmationService.ConfirmAsync(
                request.Email.Trim(),
                request.Code.Trim(),
                cancellationToken);

            return NoContent();
        }
        catch (DuplicateEmailException)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }
        catch (ExpiredConfirmationCodeException)
        {
            return BadRequest(new { message = "Confirmation code has expired." });
        }
        catch (InvalidConfirmationCodeException)
        {
            return BadRequest(new { message = "Confirmation code is invalid." });
        }
        catch (InvalidRegistrationException exception)
        {
            return BadRequest(new
            {
                message = "Registration failed.",
                errors = exception.Errors.Select(error => error.Description).ToList()
            });
        }
    }

    [HttpPost("resend-confirmation-code")]
    public async Task<ActionResult<PendingEmailRegistrationResponse>> ResendConfirmationCode(
        ResendConfirmationCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        try
        {
            var result = await emailConfirmationService.ResendAsync(
                request.Email.Trim(),
                cancellationToken);

            return Ok(ToPendingEmailRegistrationResponse(result));
        }
        catch (DuplicateEmailException)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }
        catch (ConfirmationCodeCooldownException exception)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = "Please wait before requesting a new confirmation code.", exception.ResendAvailableAtUtc });
        }
        catch (InvalidConfirmationCodeException)
        {
            return BadRequest(new { message = "No pending registration was found for this email." });
        }
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
        if (user is null || IsBlockedForAuthentication(user))
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
        if (user is null || IsBlockedForAuthentication(user))
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
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            UserNames.FormatFullName(user.FirstName, user.LastName),
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
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            UserNames.FormatFullName(user.FirstName, user.LastName),
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

    private static PendingEmailRegistrationResponse ToPendingEmailRegistrationResponse(
        PendingEmailRegistrationResult result)
    {
        return new PendingEmailRegistrationResponse(
            result.Email,
            result.CodeExpiresAtUtc,
            result.ResendAvailableAtUtc);
    }

    private static bool IsBlockedForAuthentication(ApplicationUser user)
    {
        return !user.EmailConfirmed || IsSuspended(user);
    }

    private static bool IsSuspended(ApplicationUser user)
    {
        return user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow;
    }
}

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword);

public sealed record PendingEmailRegistrationResponse(
    string Email,
    DateTime CodeExpiresAtUtc,
    DateTime ResendAvailableAtUtc);

public sealed record ConfirmEmailRequest(
    string Email,
    string Code);

public sealed record ResendConfirmationCodeRequest(
    string Email);

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
    string FirstName,
    string LastName,
    string FullName,
    IReadOnlyList<string> Roles);

public sealed record CurrentUserResponse(
    Guid Id,
    int PublicNumber,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<CurrentUserBusinessMembershipResponse> BusinessMemberships);

public sealed record CurrentUserBusinessMembershipResponse(
    Guid BusinessId,
    string BusinessName,
    string Role,
    string Status);
