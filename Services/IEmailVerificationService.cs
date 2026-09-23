using Kayane.Models;

namespace Kayane.Services;

public interface IEmailVerificationService
{
    /// <summary>
    /// Generates a verification token, stores the hash on the user, and returns the raw token.
    /// Caller must save changes to persist the token.
    /// </summary>
    string GenerateToken(User user);

    /// <summary>
    /// Sends the verification email with the given token. Best-effort — swallows failures.
    /// </summary>
    Task SendVerificationEmailAsync(User user, string rawToken, string verificationUrl);
}