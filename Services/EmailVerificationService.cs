using Kayane.Models;
using System.Security.Cryptography;
using System.Text;

namespace Kayane.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailVerificationService> _logger;

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public EmailVerificationService(
        IEmailService emailService,
        ILogger<EmailVerificationService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public string GenerateToken(User user)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        user.EmailVerificationTokenHash = ComputeSha256Hash(raw);
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.Add(TokenLifetime);

        return raw;
    }

    public async Task SendVerificationEmailAsync(User user, string rawToken, string verificationUrl)
    {
        var html = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 560px;'>
                <h2 style='color: #4f46e5;'>Verify Your Kayane Account</h2>
                <p>Hello {System.Net.WebUtility.HtmlEncode(user.Name)},</p>
                <p>Please confirm your email address by clicking the button below.</p>
                <p style='margin: 24px 0;'>
                    <a href='{verificationUrl}'
                       style='background: #4f46e5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; font-weight: bold;'>
                        Verify My Email
                    </a>
                </p>
                <p>Or paste this link into your browser:</p>
                <p style='font-size: 12px; word-break: break-all; color: #4f46e5;'>{verificationUrl}</p>
                <p style='font-size: 12px; color: #888;'>This link expires in 24 hours. If you didn't create a Kayane account, ignore this email.</p>
            </div>";

        try
        {
            await _emailService.SendEmailAsync(user.Email, "Verify Your Kayane Account", html);
            _logger.LogInformation("Verification email sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Verification email failed for {Email}", user.Email);
        }
    }

    public static string ComputeSha256Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}