namespace LoreLoom.Api.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string displayName, string token, string? culture = null);
    Task SendPasswordResetAsync(string toEmail, string displayName, string token, string? culture = null);
}
