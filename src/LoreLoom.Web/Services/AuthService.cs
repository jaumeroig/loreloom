using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace LoreLoom.Web.Services;

public class AuthService : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;

    public string? Jwt { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? AccountToken { get; private set; }
    public string Language { get; set; } = "English";

    public AuthService(IJSRuntime js, HttpClient http)
    {
        _js = js;
        _http = http;
    }

    public async Task InitializeAsync()
    {
        Jwt = await _js.InvokeAsync<string?>("localStorage.getItem", "jwt");
        DisplayName = await _js.InvokeAsync<string?>("localStorage.getItem", "displayName");
        Email = await _js.InvokeAsync<string?>("localStorage.getItem", "email");
        AccountToken = await _js.InvokeAsync<string?>("localStorage.getItem", "accountToken");
        Language = await _js.InvokeAsync<string?>("localStorage.getItem", "language") ?? "English";

        if (!string.IsNullOrEmpty(Jwt))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt);
    }

    public async Task LoginAsync(string jwt, string displayName, string email, string accountToken)
    {
        Jwt = jwt;
        DisplayName = displayName;
        Email = email;
        AccountToken = accountToken;

        await _js.InvokeVoidAsync("localStorage.setItem", "jwt", jwt);
        await _js.InvokeVoidAsync("localStorage.setItem", "displayName", displayName);
        await _js.InvokeVoidAsync("localStorage.setItem", "email", email);
        await _js.InvokeVoidAsync("localStorage.setItem", "accountToken", accountToken);

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
        Jwt = null;
        DisplayName = null;
        Email = null;
        AccountToken = null;

        await _js.InvokeVoidAsync("localStorage.removeItem", "jwt");
        await _js.InvokeVoidAsync("localStorage.removeItem", "displayName");
        await _js.InvokeVoidAsync("localStorage.removeItem", "email");
        await _js.InvokeVoidAsync("localStorage.removeItem", "accountToken");

        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SetLanguageAsync(string language)
    {
        Language = language;
        await _js.InvokeVoidAsync("localStorage.setItem", "language", language);
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
}
