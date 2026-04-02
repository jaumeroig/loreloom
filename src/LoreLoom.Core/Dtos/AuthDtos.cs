using System.ComponentModel.DataAnnotations;

namespace LoreLoom.Core.Dtos;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(3), MaxLength(50)] string DisplayName,
    [Required, MinLength(8)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record UpdateDisplayNameRequest(
    [Required, MinLength(3), MaxLength(50)] string DisplayName
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword,
    [Required] string ConfirmNewPassword
);

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword,
    [Required] string ConfirmNewPassword
);

public record AuthResponse(
    string DisplayName,
    string Email,
    string Token,
    string? Jwt = null,
    bool EmailVerified = true
);
