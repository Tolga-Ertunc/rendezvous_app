namespace Rendezvous.Infrastructure.Identity;

public class PendingEmailRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public required string ConfirmationCodeHash { get; set; }
    public DateTime CodeExpiresAtUtc { get; set; }
    public DateTime LastSentAtUtc { get; set; }
    public int FailedAttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
