using System.Security.Cryptography;
using System.Text;
using LoreLoom.Api.Extensions;
using LoreLoom.Api.Services;
using LoreLoom.Core.Data;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Localization;
using LoreLoom.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(LoreLoomDbContext db, JwtService jwtService, IEmailService emailService, IAppTextLocalizer text) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await db.Accounts.AnyAsync(a => a.Email == request.Email))
            return Conflict(text["api_email_registered"]);

        var verificationToken = GenerateToken();
        var preferredCulture = AppCultures.Normalize(Request.Headers["X-Culture"].ToString());

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = HashPassword(request.Password),
            PreferredCulture = preferredCulture,
            EmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        await emailService.SendEmailVerificationAsync(account.Email, account.DisplayName, verificationToken, account.PreferredCulture);

        var jwt = jwtService.GenerateToken(account);
        return CreatedAtAction(nameof(Register),
            new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified, account.PreferredCulture));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
        if (account is null)
            return Unauthorized(text["api_invalid_credentials"]);

        if (!VerifyPassword(request.Password, account.PasswordHash))
            return Unauthorized(text["api_invalid_credentials"]);

        if (!account.EmailVerified)
            return StatusCode(403, text["api_verify_before_login"]);

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified, account.PreferredCulture);
    }

    [HttpGet("verify-email")]
    public async Task<ActionResult> VerifyEmail([FromQuery] string token)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.EmailVerificationToken == token);

        if (account is null || account.EmailVerificationTokenExpiry < DateTime.UtcNow)
            return BadRequest(text["api_invalid_verification_link"]);

        account.EmailVerified = true;
        account.EmailVerificationToken = null;
        account.EmailVerificationTokenExpiry = null;
        await db.SaveChangesAsync();

        return Ok(text["api_email_verified"]);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest? request)
    {
        Account? account;

        if (User.Identity?.IsAuthenticated == true)
        {
            var accountId = this.GetAccountId();
            if (!accountId.HasValue)
                return Unauthorized();

            account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId.Value);
            if (account is null)
                return Unauthorized();

            if (account.EmailVerified)
                return BadRequest(text["api_email_already_verified"]);
        }
        else
        {
            var email = request?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(text["api_email_required"]);

            account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == email);
            if (account is null || account.EmailVerified)
                return Ok(text["api_verification_if_registered"]);
        }

        account.EmailVerificationToken = GenerateToken();
        account.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        await db.SaveChangesAsync();

        await emailService.SendEmailVerificationAsync(account.Email, account.DisplayName, account.EmailVerificationToken, account.PreferredCulture);

        return Ok(User.Identity?.IsAuthenticated == true
            ? text["api_verification_sent"]
            : text["api_verification_if_registered"]);
    }

    [Authorize]
    [HttpPut("profile/display-name")]
    public async Task<ActionResult<AuthResponse>> UpdateDisplayName(UpdateDisplayNameRequest request)
    {
        var accountId = this.GetAccountId();
        if (!accountId.HasValue)
            return Unauthorized();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId.Value);
        if (account is null)
            return Unauthorized();

        account.DisplayName = request.DisplayName.Trim();
        await db.SaveChangesAsync();

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified, account.PreferredCulture);
    }

    [Authorize]
    [HttpPut("profile/culture")]
    public async Task<ActionResult<AuthResponse>> UpdateCulture(UpdateCultureRequest request)
    {
        var accountId = this.GetAccountId();
        if (!accountId.HasValue)
            return Unauthorized();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId.Value);
        if (account is null)
            return Unauthorized();

        account.PreferredCulture = AppCultures.Normalize(request.Culture);
        await db.SaveChangesAsync();

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified, account.PreferredCulture);
    }

    [Authorize]
    [HttpPut("profile/password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var accountId = this.GetAccountId();
        if (!accountId.HasValue)
            return Unauthorized();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId.Value);
        if (account is null)
            return Unauthorized();

        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest(text["api_new_passwords_no_match"]);

        if (!VerifyPassword(request.CurrentPassword, account.PasswordHash))
            return BadRequest(text["api_current_password_incorrect"]);

        if (VerifyPassword(request.NewPassword, account.PasswordHash))
            return BadRequest(text["api_new_password_must_differ"]);

        account.PasswordHash = HashPassword(request.NewPassword);
        await db.SaveChangesAsync();

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified, account.PreferredCulture);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);

        if (account is not null)
        {
            account.PasswordResetToken = GenerateToken();
            account.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync();

            await emailService.SendPasswordResetAsync(account.Email, account.DisplayName, account.PasswordResetToken, account.PreferredCulture);
        }

        return Ok(text["api_password_reset_if_registered"]);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest(text["api_passwords_no_match"]);

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.PasswordResetToken == request.Token);

        if (account is null || account.PasswordResetTokenExpiry < DateTime.UtcNow)
            return BadRequest(text["api_invalid_reset_link"]);

        account.PasswordHash = HashPassword(request.NewPassword);
        account.PasswordResetToken = null;
        account.PasswordResetTokenExpiry = null;
        await db.SaveChangesAsync();

        return Ok(text["api_password_reset_success"]);
    }

    private static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
