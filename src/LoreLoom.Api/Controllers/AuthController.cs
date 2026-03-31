using System.Security.Cryptography;
using System.Text;
using LoreLoom.Api.Services;
using LoreLoom.Core.Data;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(LoreLoomDbContext db, JwtService jwtService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await db.Accounts.AnyAsync(a => a.Email == request.Email))
            return Conflict("Email already registered.");

        if (await db.Accounts.AnyAsync(a => a.Username == request.Username))
            return Conflict("Username already taken.");

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Username = request.Username,
            PasswordHash = HashPassword(request.Password)
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var jwt = jwtService.GenerateToken(account);
        return CreatedAtAction(nameof(Register),
            new AuthResponse(account.Username, account.Email, account.Token, jwt));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
        if (account is null)
            return Unauthorized("Invalid email or password.");

        if (!VerifyPassword(request.Password, account.PasswordHash))
            return Unauthorized("Invalid email or password.");

        var jwt = jwtService.GenerateToken(account);
        return new AuthResponse(account.Username, account.Email, account.Token, jwt);
    }

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
