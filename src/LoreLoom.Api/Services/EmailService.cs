using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LoreLoom.Api.Services;

public class EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SmtpHost);

    public async Task SendEmailVerificationAsync(string toEmail, string displayName, string token)
    {
        var verifyUrl = $"{_options.BaseUrl}/verify-email?token={Uri.EscapeDataString(token)}";

        if (!IsConfigured)
        {
            logger.LogInformation(
                "[DEV] Email verification token for {Email}: {Token} | Link: {Url}",
                toEmail, token, verifyUrl);
            return;
        }

        var body = $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#7B68EE;">Welcome to LoreLoom, {displayName}!</h2>
              <p>Please verify your email address to activate your account.</p>
              <p>
                <a href="{verifyUrl}"
                   style="background:#7B68EE;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;">
                  Verify Email
                </a>
              </p>
              <p style="color:#888;font-size:0.85em;">This link expires in 24 hours. If you didn't create a LoreLoom account, you can ignore this email.</p>
            </body></html>
            """;

        await SendAsync(toEmail, displayName, "Verify your LoreLoom email", body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string displayName, string token)
    {
        var resetUrl = $"{_options.BaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

        if (!IsConfigured)
        {
            logger.LogInformation(
                "[DEV] Password reset token for {Email}: {Token} | Link: {Url}",
                toEmail, token, resetUrl);
            return;
        }

        var body = $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#7B68EE;">Reset your LoreLoom password</h2>
              <p>Hi {displayName}, we received a request to reset your password.</p>
              <p>
                <a href="{resetUrl}"
                   style="background:#7B68EE;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;">
                  Reset Password
                </a>
              </p>
              <p style="color:#888;font-size:0.85em;">This link expires in 1 hour. If you didn't request a password reset, you can ignore this email.</p>
            </body></html>
            """;

        await SendAsync(toEmail, displayName, "Reset your LoreLoom password", body);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var secureSocketOptions = _options.EnableSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secureSocketOptions);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPassword);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
