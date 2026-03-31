using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoreLoom.Core.Dtos;

namespace LoreLoom.Cli;

public class ApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    // Auth
    public async Task<AuthResponse?> Register(RegisterRequest request)
        => await PostAsync<AuthResponse>("auth/register", request);

    public async Task<AuthResponse?> Login(LoginRequest request)
        => await PostAsync<AuthResponse>("auth/login", request);

    // Characters
    public async Task<CharacterResponse?> CreateCharacter(CreateCharacterRequest request)
        => await PostAsync<CharacterResponse>("characters", request);

    public async Task<List<CharacterResponse>> GetCharacters(string token)
        => await GetAsync<List<CharacterResponse>>($"characters/{token}") ?? [];

    // Games
    public async Task<List<GameResponse>> ListGames()
        => await GetAsync<List<GameResponse>>("games?isPublic=true&status=Waiting") ?? [];

    public async Task<GameResponse?> GetGame(Guid id)
        => await GetAsync<GameResponse>($"games/{id}");

    public async Task<GameResponse?> CreateGame(CreateGameRequest request)
        => await PostAsync<GameResponse>("games", request);

    public async Task<GameResponse?> JoinGame(Guid gameId, JoinGameRequest request)
        => await PostAsync<GameResponse>($"games/{gameId}/join", request);

    public async Task<GameResponse?> StartGame(Guid gameId, StartGameRequest request)
        => await PostAsync<GameResponse>($"games/{gameId}/start", request);

    public async Task<TurnResultResponse?> SendTurn(Guid gameId, SendTurnRequest request)
        => await PostAsync<TurnResultResponse>($"games/{gameId}/turns", request);

    public async Task<List<TurnResponse>> GetTurns(Guid gameId)
        => await GetAsync<List<TurnResponse>>($"games/{gameId}/turns") ?? [];

    public async Task<GameResultResponse?> GetResult(Guid gameId)
        => await GetAsync<GameResultResponse>($"games/{gameId}/result");

    // Ranking
    public async Task<List<RankingEntry>> GetRanking()
        => await GetAsync<List<RankingEntry>>("ranking") ?? [];

    public async Task<string?> ExportGame(Guid gameId)
    {
        var response = await _http.GetAsync($"games/{gameId}/export");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<T?> GetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task<T?> PostAsync<T>(string path, object body)
    {
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}
