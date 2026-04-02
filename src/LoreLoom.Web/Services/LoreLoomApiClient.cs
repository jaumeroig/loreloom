using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoreLoom.Core.Dtos;

namespace LoreLoom.Web.Services;

public class LoreLoomApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    // Auth
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        => await PostAsync<AuthResponse>("auth/register", request);

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        => await PostAsync<AuthResponse>("auth/login", request);

    public async Task<AuthResponse?> UpdateDisplayNameAsync(UpdateDisplayNameRequest request)
        => await PutAsync<AuthResponse>("auth/profile/display-name", request);

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var response = await http.GetAsync($"auth/verify-email?token={Uri.EscapeDataString(token)}");
        return response.IsSuccessStatusCode;
    }

    public async Task ResendVerificationAsync(string? email = null)
        => await PostVoidAsync("auth/resend-verification", email is null ? null : new ResendVerificationRequest(email));

    public async Task<AuthResponse?> ChangePasswordAsync(ChangePasswordRequest request)
        => await PutAsync<AuthResponse>("auth/profile/password", request);

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
        => await PostVoidAsync("auth/forgot-password", request);

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
        => await PostVoidAsync("auth/reset-password", request);

    // Characters
    public async Task<CharacterResponse?> CreateCharacterAsync(CreateCharacterRequest request)
        => await PostAsync<CharacterResponse>("characters", request);

    public async Task<List<CharacterResponse>> GetMyCharactersAsync()
        => await GetAsync<List<CharacterResponse>>("characters/me") ?? [];

    // Games
    public async Task<List<GameResponse>> ListGamesAsync(string? status = null, bool? isPublic = null)
    {
        var query = "games?";
        if (status is not null) query += $"status={status}&";
        if (isPublic.HasValue) query += $"isPublic={isPublic.Value}";
        return await GetAsync<List<GameResponse>>(query.TrimEnd('&', '?')) ?? [];
    }

    public async Task<GameResponse?> GetGameAsync(Guid id)
        => await GetAsync<GameResponse>($"games/{id}");

    public async Task<GameResponse?> CreateGameAsync(CreateGameRequest request)
        => await PostAsync<GameResponse>("games", request);

    public async Task<GameResponse?> JoinGameAsync(Guid gameId, JoinGameRequest request)
        => await PostAsync<GameResponse>($"games/{gameId}/join", request);

    public async Task<GameResponse?> StartGameAsync(Guid gameId, StartGameRequest request)
        => await PostAsync<GameResponse>($"games/{gameId}/start", request);

    public async Task DeleteGameAsync(Guid gameId)
        => await DeleteAsync($"games/{gameId}");

    public async Task<TurnResultResponse?> SendTurnAsync(Guid gameId, SendTurnRequest request)
        => await PostAsync<TurnResultResponse>($"games/{gameId}/turns", request);

    public async Task<List<TurnResponse>> GetTurnsAsync(Guid gameId)
        => await GetAsync<List<TurnResponse>>($"games/{gameId}/turns") ?? [];

    public async Task<GameResultResponse?> GetResultAsync(Guid gameId)
        => await GetAsync<GameResultResponse>($"games/{gameId}/result");

    public async Task<string?> ExportGameAsync(Guid gameId)
    {
        var response = await http.GetAsync($"games/{gameId}/export");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    // Ranking
    public async Task<List<RankingEntry>> GetRankingAsync()
        => await GetAsync<List<RankingEntry>>("ranking") ?? [];

    private async Task<T?> GetAsync<T>(string path)
    {
        var response = await http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task<T?> PostAsync<T>(string path, object body)
    {
        var response = await http.PostAsJsonAsync(path, body, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task DeleteAsync(string path)
    {
        var response = await http.DeleteAsync(path);
        response.EnsureSuccessStatusCode();
    }
  
    private async Task<T?> PutAsync<T>(string path, object body)
    {
        var response = await http.PutAsJsonAsync(path, body, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task PostVoidAsync(string path, object? body = null)
    {
        HttpResponseMessage response;
        if (body is null)
            response = await http.PostAsync(path, null);
        else
            response = await http.PostAsJsonAsync(path, body, JsonOptions);
        response.EnsureSuccessStatusCode();
    }
}
