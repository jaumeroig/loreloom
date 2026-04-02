using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Globalization;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Localization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace LoreLoom.Web.Services;

public class AuthService : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private bool _initialized;

    public string? Jwt { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? AccountToken { get; private set; }
    public string Language { get; private set; } = AppCultures.DefaultCulture;
    public bool EmailVerified { get; private set; } = true;

    public AuthService(IJSRuntime js, HttpClient http)
    {
        _js = js;
        _http = http;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        Jwt = await _js.InvokeAsync<string?>("localStorage.getItem", "jwt");
        DisplayName = await _js.InvokeAsync<string?>("localStorage.getItem", "displayName");
        Email = await _js.InvokeAsync<string?>("localStorage.getItem", "email");
        AccountToken = await _js.InvokeAsync<string?>("localStorage.getItem", "accountToken");
        var storedCulture = await _js.InvokeAsync<string?>("localStorage.getItem", "language");
        var emailVerifiedRaw = await _js.InvokeAsync<string?>("localStorage.getItem", "emailVerified");
        EmailVerified = emailVerifiedRaw != "false";

        Language = AppCultures.Normalize(storedCulture);

        if (!string.IsNullOrEmpty(Jwt))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt);
            var preferredCulture = ParseClaimsFromJwt(Jwt)
                .FirstOrDefault(claim => claim.Type == "preferred_culture")
                ?.Value;
            if (!string.IsNullOrWhiteSpace(preferredCulture))
                Language = AppCultures.Normalize(preferredCulture);
        }
        else
        {
            var browserCulture = await _js.InvokeAsync<string?>("loreLoom.getBrowserCulture");
            Language = AppCultures.Normalize(storedCulture ?? browserCulture);
        }

        await _js.InvokeVoidAsync("localStorage.setItem", "language", Language);
        ApplyCulture(Language);
        _initialized = true;
    }

    public async Task LoginAsync(string jwt, string displayName, string email, string accountToken, bool emailVerified = true, string? preferredCulture = null)
    {
        Jwt = jwt;
        DisplayName = displayName;
        Email = email;
        AccountToken = accountToken;
        EmailVerified = emailVerified;
        Language = AppCultures.Normalize(preferredCulture ?? Language);

        await _js.InvokeVoidAsync("localStorage.setItem", "jwt", jwt);
        await _js.InvokeVoidAsync("localStorage.setItem", "displayName", displayName);
        await _js.InvokeVoidAsync("localStorage.setItem", "email", email);
        await _js.InvokeVoidAsync("localStorage.setItem", "accountToken", accountToken);
        await _js.InvokeVoidAsync("localStorage.setItem", "emailVerified", emailVerified.ToString().ToLower());
        await _js.InvokeVoidAsync("localStorage.setItem", "language", Language);
        ApplyCulture(Language);

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
        Jwt = null;
        DisplayName = null;
        Email = null;
        AccountToken = null;
        EmailVerified = true;

        await _js.InvokeVoidAsync("localStorage.removeItem", "jwt");
        await _js.InvokeVoidAsync("localStorage.removeItem", "displayName");
        await _js.InvokeVoidAsync("localStorage.removeItem", "email");
        await _js.InvokeVoidAsync("localStorage.removeItem", "accountToken");
        await _js.InvokeVoidAsync("localStorage.removeItem", "emailVerified");
        var browserCulture = await _js.InvokeAsync<string?>("loreLoom.getBrowserCulture");
        Language = AppCultures.Normalize(browserCulture);
        await _js.InvokeVoidAsync("localStorage.setItem", "language", Language);
        ApplyCulture(Language);

        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SetLanguageAsync(string culture)
    {
        Language = AppCultures.Normalize(culture);
        await _js.InvokeVoidAsync("localStorage.setItem", "language", Language);
        ApplyCulture(Language);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task UpdateSessionAsync(AuthResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Jwt))
            throw new InvalidOperationException("JWT is required to update the authenticated session.");

        await LoginAsync(response.Jwt, response.DisplayName, response.Email, response.Token, response.EmailVerified, response.PreferredCulture);
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrEmpty(Jwt))
        {
            await InitializeAsync();
        }

        if (string.IsNullOrEmpty(Jwt))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = ParseClaimsFromJwt(Jwt);
        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var kvPairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
        if (kvPairs is null) yield break;

        foreach (var kvp in kvPairs)
        {
            yield return new Claim(kvp.Key, kvp.Value.ToString()!);
        }
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    private void ApplyCulture(string culture)
    {
        var cultureInfo = CultureInfo.GetCultureInfo(AppCultures.Normalize(culture));
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
        _js.InvokeVoidAsync("loreLoom.setDocumentLanguage", cultureInfo.Name);
    }
}
