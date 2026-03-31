using System.ComponentModel.DataAnnotations;

namespace LoreLoom.Core.Dtos;

public record RegisterRequest(
    [Required, MinLength(3), MaxLength(50)] string Username,
    [Required, MinLength(4)] string Password
);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record AuthResponse(
    string Username,
    string Token
);
