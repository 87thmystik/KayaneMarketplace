using System.Net;
using System.Net.Mail;

namespace Kayane.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string htmlMessage)
    {
        var smtpHost = _config["Smtp:Host"];
        var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
        var smtpUser = _config["Smtp:User"];
        var smtpPass = _config["Smtp:Pass"];
        var senderEmail = _config["Smtp:FromEmail"] ?? smtpUser;
        var senderName = _config["Smtp:FromName"] ?? "Kayane Marketplace";

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(senderEmail!, senderName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };

        mailMessage.To.Add(recipientEmail);
        await client.SendMailAsync(mailMessage);
    }
}