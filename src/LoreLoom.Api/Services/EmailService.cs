using MailKit.Net.Smtp;
using MailKit.Security;
using LoreLoom.Core.Localization;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LoreLoom.Api.Services;

public class EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger, IAppTextLocalizer text) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SmtpHost);

    public async Task SendEmailVerificationAsync(string toEmail, string displayName, string token, string? culture = null)
    {
        var verifyUrl = $"{_options.BaseUrl}/verify-email?token={Uri.EscapeDataString(token)}";
        var resolvedCulture = AppCultures.Normalize(culture);

        if (!IsConfigured)
        {
            logger.LogInformation(
                "[DEV] Email verification token for {Email}: {Token} | Link: {Url}",
                toEmail, token, verifyUrl);
            return;
        }

        var body = $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#7B68EE;">{text.FormatForCulture(resolvedCulture, "email_verify_heading", displayName)}</h2>
              <p>{text.Get("email_verify_body", resolvedCulture)}</p>
              <p>
                <a href="{verifyUrl}"
                   style="background:#7B68EE;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;">
                  {text.Get("email_verify_button", resolvedCulture)}
                </a>
              </p>
              <p style="color:#888;font-size:0.85em;">{text.Get("email_verify_footer", resolvedCulture)}</p>
            </body></html>
            """;

        await SendAsync(toEmail, displayName, text.Get("email_verify_subject", resolvedCulture), body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string displayName, string token, string? culture = null)
    {
        var resetUrl = $"{_options.BaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
        var resolvedCulture = AppCultures.Normalize(culture);

        if (!IsConfigured)
        {
            logger.LogInformation(
                "[DEV] Password reset token for {Email}: {Token} | Link: {Url}",
                toEmail, token, resetUrl);
            return;
        }

        var body = $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#7B68EE;">{text.Get("email_reset_heading", resolvedCulture)}</h2>
              <p>{text.FormatForCulture(resolvedCulture, "email_reset_body", displayName)}</p>
              <p>
                <a href="{resetUrl}"
                   style="background:#7B68EE;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;">
                  {text.Get("email_reset_button", resolvedCulture)}
                </a>
              </p>
              <p style="color:#888;font-size:0.85em;">{text.Get("email_reset_footer", resolvedCulture)}</p>
            </body></html>
            """;

        await SendAsync(toEmail, displayName, text.Get("email_reset_subject", resolvedCulture), body);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var secureSocketOptions = _options.SmtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : _options.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secureSocketOptions);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPassword);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
