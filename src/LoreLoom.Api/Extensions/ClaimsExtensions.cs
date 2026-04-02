using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace LoreLoom.Api.Extensions;

public static class ClaimsExtensions
{
    public static string? GetAccountToken(this ControllerBase controller)
        => controller.User.FindFirstValue("account_token");

    public static string? GetDisplayName(this ControllerBase controller)
        => controller.User.FindFirstValue(ClaimTypes.Name);

    public static string? GetPreferredCulture(this ControllerBase controller)
        => controller.User.FindFirstValue("preferred_culture");

    public static Guid? GetAccountId(this ControllerBase controller)
    {
        var sub = controller.User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
