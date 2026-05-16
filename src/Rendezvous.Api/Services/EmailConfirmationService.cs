using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rendezvous.Api.Authentication;
using Rendezvous.Api.Email;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class EmailConfirmationService
{
    public const int CodeLength = 6;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxFailedAttempts = 5;

    private readonly AppDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IPasswordHasher<ApplicationUser> passwordHasher;
    private readonly PublicNumberGenerator publicNumberGenerator;
    private readonly IEmailSender emailSender;
    private readonly JwtOptions jwtOptions;

    public EmailConfirmationService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        PublicNumberGenerator publicNumberGenerator,
        IEmailSender emailSender,
        IOptions<JwtOptions> jwtOptions)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.passwordHasher = passwordHasher;
        this.publicNumberGenerator = publicNumberGenerator;
        this.emailSender = emailSender;
        this.jwtOptions = jwtOptions.Value;
    }

    public async Task<PendingEmailRegistrationResult> StartAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email);
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            throw new DuplicateEmailException();
        }

        var normalizedFirstName = UserNames.Normalize(firstName);
        var normalizedLastName = UserNames.Normalize(lastName);

        var passwordValidation = await ValidatePasswordAsync(
            email,
            normalizedFirstName,
            normalizedLastName,
            password);
        if (!passwordValidation.Succeeded)
        {
            throw new InvalidRegistrationException(passwordValidation.Errors);
        }

        var nowUtc = DateTime.UtcNow;
        var pendingRegistration = await dbContext.PendingEmailRegistrations
            .SingleOrDefaultAsync(
                registration => registration.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (pendingRegistration is not null
            && pendingRegistration.LastSentAtUtc.Add(ResendCooldown) > nowUtc)
        {
            throw new ConfirmationCodeCooldownException(
                pendingRegistration.LastSentAtUtc.Add(ResendCooldown));
        }

        var code = GenerateCode();
        var codeHash = HashCode(normalizedEmail, code);
        var tempUser = CreateUser(email, normalizedFirstName, normalizedLastName);
        var passwordHash = passwordHasher.HashPassword(tempUser, password);

        if (pendingRegistration is null)
        {
            pendingRegistration = new PendingEmailRegistration
            {
                Email = email,
                NormalizedEmail = normalizedEmail,
                FirstName = normalizedFirstName,
                LastName = normalizedLastName,
                PasswordHash = passwordHash,
                ConfirmationCodeHash = codeHash,
                CodeExpiresAtUtc = nowUtc.Add(CodeLifetime),
                LastSentAtUtc = nowUtc,
                FailedAttemptCount = 0,
                CreatedAtUtc = nowUtc
            };
            dbContext.PendingEmailRegistrations.Add(pendingRegistration);
        }
        else
        {
            pendingRegistration.Email = email;
            pendingRegistration.FirstName = normalizedFirstName;
            pendingRegistration.LastName = normalizedLastName;
            pendingRegistration.PasswordHash = passwordHash;
            pendingRegistration.ConfirmationCodeHash = codeHash;
            pendingRegistration.CodeExpiresAtUtc = nowUtc.Add(CodeLifetime);
            pendingRegistration.LastSentAtUtc = nowUtc;
            pendingRegistration.FailedAttemptCount = 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendConfirmationCodeAsync(email, code, cancellationToken);

        return ToResult(pendingRegistration);
    }

    public async Task<ApplicationUser> ConfirmAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email);
        var pendingRegistration = await dbContext.PendingEmailRegistrations
            .SingleOrDefaultAsync(
                registration => registration.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (pendingRegistration is null)
        {
            throw new InvalidConfirmationCodeException();
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            dbContext.PendingEmailRegistrations.Remove(pendingRegistration);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new DuplicateEmailException();
        }

        var nowUtc = DateTime.UtcNow;
        if (pendingRegistration.CodeExpiresAtUtc <= nowUtc)
        {
            throw new ExpiredConfirmationCodeException();
        }

        if (pendingRegistration.FailedAttemptCount >= MaxFailedAttempts
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(pendingRegistration.ConfirmationCodeHash),
                Encoding.UTF8.GetBytes(HashCode(normalizedEmail, code))))
        {
            pendingRegistration.FailedAttemptCount += 1;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidConfirmationCodeException();
        }

        if (!UserNames.IsValidNamePart(pendingRegistration.FirstName)
            || !UserNames.IsValidNamePart(pendingRegistration.LastName))
        {
            throw new InvalidRegistrationException(
            [
                new IdentityError
                {
                    Description = "First name and last name are required."
                }
            ]);
        }

        var user = CreateUser(
            pendingRegistration.Email,
            UserNames.Normalize(pendingRegistration.FirstName ?? string.Empty),
            UserNames.Normalize(pendingRegistration.LastName ?? string.Empty));
        user.PublicNumber = await publicNumberGenerator.GenerateAsync(cancellationToken);
        user.EmailConfirmed = true;
        user.PasswordHash = pendingRegistration.PasswordHash;

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            throw new InvalidRegistrationException(createResult.Errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, ApplicationRoles.User);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new InvalidRegistrationException(roleResult.Errors);
        }

        dbContext.PendingEmailRegistrations.Remove(pendingRegistration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<PendingEmailRegistrationResult> ResendAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email);
        var pendingRegistration = await dbContext.PendingEmailRegistrations
            .SingleOrDefaultAsync(
                registration => registration.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (pendingRegistration is null)
        {
            throw new InvalidConfirmationCodeException();
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            dbContext.PendingEmailRegistrations.Remove(pendingRegistration);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new DuplicateEmailException();
        }

        var nowUtc = DateTime.UtcNow;
        if (pendingRegistration.LastSentAtUtc.Add(ResendCooldown) > nowUtc)
        {
            throw new ConfirmationCodeCooldownException(
                pendingRegistration.LastSentAtUtc.Add(ResendCooldown));
        }

        var code = GenerateCode();
        pendingRegistration.ConfirmationCodeHash = HashCode(normalizedEmail, code);
        pendingRegistration.CodeExpiresAtUtc = nowUtc.Add(CodeLifetime);
        pendingRegistration.LastSentAtUtc = nowUtc;
        pendingRegistration.FailedAttemptCount = 0;

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendConfirmationCodeAsync(pendingRegistration.Email, code, cancellationToken);

        return ToResult(pendingRegistration);
    }

    private async Task<IdentityResult> ValidatePasswordAsync(
        string email,
        string firstName,
        string lastName,
        string password)
    {
        var user = CreateUser(email, firstName, lastName);
        var errors = new List<IdentityError>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        return errors.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed(errors.ToArray());
    }

    private Task SendConfirmationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        return emailSender.SendAsync(
            new EmailMessage(
                email,
                "Your Rendezvous confirmation code",
                $"Your Rendezvous confirmation code is {code}. It expires in 15 minutes.",
                $"<p>Your Rendezvous confirmation code is <strong>{code}</strong>.</p><p>It expires in 15 minutes.</p>"),
            cancellationToken);
    }

    private string HashCode(string normalizedEmail, string code)
    {
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey is required for confirmation code hashing.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{normalizedEmail}:{code}"));

        return Convert.ToHexString(bytes);
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString($"D{CodeLength}", CultureInfo.InvariantCulture);
    }

    private static ApplicationUser CreateUser(
        string email,
        string firstName,
        string lastName)
    {
        return new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };
    }

    private static PendingEmailRegistrationResult ToResult(
        PendingEmailRegistration pendingRegistration)
    {
        return new PendingEmailRegistrationResult(
            pendingRegistration.Email,
            pendingRegistration.CodeExpiresAtUtc,
            pendingRegistration.LastSentAtUtc.Add(ResendCooldown));
    }
}

public sealed record PendingEmailRegistrationResult(
    string Email,
    DateTime CodeExpiresAtUtc,
    DateTime ResendAvailableAtUtc);

public class DuplicateEmailException : Exception;

public class InvalidConfirmationCodeException : Exception;

public class ExpiredConfirmationCodeException : Exception;

public class ConfirmationCodeCooldownException : Exception
{
    public ConfirmationCodeCooldownException(DateTime resendAvailableAtUtc)
    {
        ResendAvailableAtUtc = resendAvailableAtUtc;
    }

    public DateTime ResendAvailableAtUtc { get; }
}

public class InvalidRegistrationException : Exception
{
    public InvalidRegistrationException(IEnumerable<IdentityError> errors)
    {
        Errors = errors.ToList();
    }

    public IReadOnlyList<IdentityError> Errors { get; }
}
