namespace LoreLoom.Core.Models;

public class Account
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public string PreferredCulture { get; set; } = Localization.AppCultures.DefaultCulture;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
}
