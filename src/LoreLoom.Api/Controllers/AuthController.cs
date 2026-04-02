using System.Security.Cryptography;
using System.Text;
using LoreLoom.Api.Extensions;
using LoreLoom.Api.Services;
using LoreLoom.Core.Data;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(LoreLoomDbContext db, JwtService jwtService, IEmailService emailService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await db.Accounts.AnyAsync(a => a.Email == request.Email))
            return Conflict("Email already registered.");

        var verificationToken = GenerateToken();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = HashPassword(request.Password),
            EmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        await emailService.SendEmailVerificationAsync(account.Email, account.DisplayName, verificationToken);

        var jwt = jwtService.GenerateToken(account);
        return CreatedAtAction(nameof(Register),
            new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
        if (account is null)
            return Unauthorized("Invalid email or password.");

        if (!VerifyPassword(request.Password, account.PasswordHash))
            return Unauthorized("Invalid email or password.");

        if (!account.EmailVerified)
            return StatusCode(403, "Please verify your email address before logging in.");

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified);
    }

    [HttpGet("verify-email")]
    public async Task<ActionResult> VerifyEmail([FromQuery] string token)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.EmailVerificationToken == token);

        if (account is null || account.EmailVerificationTokenExpiry < DateTime.UtcNow)
            return BadRequest("Invalid or expired verification link.");

        account.EmailVerified = true;
        account.EmailVerificationToken = null;
        account.EmailVerificationTokenExpiry = null;
        await db.SaveChangesAsync();

        return Ok("Email verified successfully.");
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
                return BadRequest("Email is already verified.");
        }
        else
        {
            var email = request?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == email);
            if (account is null || account.EmailVerified)
                return Ok("If that email is registered and not yet verified, a verification email has been sent.");
        }

        account.EmailVerificationToken = GenerateToken();
        account.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        await db.SaveChangesAsync();

        await emailService.SendEmailVerificationAsync(account.Email, account.DisplayName, account.EmailVerificationToken);

        return Ok(User.Identity?.IsAuthenticated == true
            ? "Verification email sent."
            : "If that email is registered and not yet verified, a verification email has been sent.");
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
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified);
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
            return BadRequest("New passwords do not match.");

        if (!VerifyPassword(request.CurrentPassword, account.PasswordHash))
            return BadRequest("Current password is incorrect.");

        if (VerifyPassword(request.NewPassword, account.PasswordHash))
            return BadRequest("New password must be different from the current password.");

        account.PasswordHash = HashPassword(request.NewPassword);
        await db.SaveChangesAsync();

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.DisplayName, account.Email, account.Token, jwt, account.EmailVerified);
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

            await emailService.SendPasswordResetAsync(account.Email, account.DisplayName, account.PasswordResetToken);
        }

        return Ok("If that email is registered, a password reset link has been sent.");
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest("Passwords do not match.");

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.PasswordResetToken == request.Token);

        if (account is null || account.PasswordResetTokenExpiry < DateTime.UtcNow)
            return BadRequest("Invalid or expired reset link.");

        account.PasswordHash = HashPassword(request.NewPassword);
        account.PasswordResetToken = null;
        account.PasswordResetTokenExpiry = null;
        await db.SaveChangesAsync();

        return Ok("Password reset successfully.");
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
