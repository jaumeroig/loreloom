# Copilot Instructions for LoreLoom

## What is LoreLoom?

LoreLoom is a multiplayer turn-based tabletop RPG where an LLM acts as the Dungeon Master. Players submit narrative actions, and Groq's llama-3.3-70b generates DM responses including narrative text, resource costs, XP awards, and victory/defeat conditions — all as structured JSON.

## Architecture

Four-project .NET 10 solution (`LoreLoom.slnx`):

- **LoreLoom.Core** — Shared library: EF Core models, DTOs (records), enums, game engine (`TurnManager`, `ContextBuilder`, `ResourceTracker`), and `ILlmService`/`GroqLlmService`.
- **LoreLoom.Api** — ASP.NET Core REST API. Thin controllers that delegate to Core. Serves the Blazor WASM client as static files. Auto-runs EF Core migrations on startup. JWT authentication. Deployed to Azure App Service.
- **LoreLoom.Web** — Blazor WebAssembly client with MudBlazor. Consumes the API via REST (`LoreLoomApiClient`). Hosted from the same App Service as the API. Custom `AuthService` (extends `AuthenticationStateProvider`) manages JWT in localStorage.
- **LoreLoom.Cli** — Legacy Spectre.Console terminal client (being phased out in favour of the web client).

### Hosting model

The API project references the Web project and serves it via `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html")`. A single `dotnet publish` of the API produces a deployable that includes both the API and the WASM client. Both run from the same Azure App Service.

### Core game loop

1. Player submits an action → `POST /games/{id}/turns`
2. `TurnManager.ProcessTurnAsync` validates it's the player's turn
3. `ContextBuilder` assembles the LLM prompt: system prompt (rules, stats, resource mechanics, language) + message history (sliding window of last 10 turns, with optional session summary)
4. `GroqLlmService` calls Groq API → returns `LlmResponse` (narrative, resource cost, victory flag, XP awards)
5. `ResourceTracker` applies cost; if resource ≤ 0 the game ends
6. Turn advances round-robin to next player by ordered Player ID
7. Every 15 turns, `ContextBuilder.BuildSummaryRequest` auto-summarizes the session to compress context

### Key game mechanics

- **No HP** — players share a party resource (customizable name, e.g. "Hope") that depletes with risky actions. Game ends when it hits 0% or the LLM declares victory.
- **Stats**: Strength, Wit, Charisma (1–5 each, must sum to 9). Simulated d6 + stat rolls (≥7 success, 5–6 partial, ≤4 fail).
- **XP/Leveling**: Characters gain XP from game results; level up at 100 × current level.

## Build & Run

```bash
# Build entire solution
dotnet build

# Run API + Web client together (port 5000)
dotnet run --project src/LoreLoom.Api

# EF Core migrations (run from repo root)
dotnet ef migrations add <Name> --project src/LoreLoom.Core --startup-project src/LoreLoom.Api
```

There is no test project yet.

## Authentication

- JWT-based. Login/register return a JWT in `AuthResponse.Jwt`.
- JWT claims include: `sub` (account ID), `email`, `name` (username), `account_token` (opaque internal token).
- The web client stores the JWT in `localStorage` and sends it via `Authorization: Bearer` header.
- Controllers extract user identity from JWT claims via `ClaimsExtensions` (`GetAccountToken()`, `GetUsername()`), with fallback to body tokens for CLI backward compatibility.
- Passwords hashed with PBKDF2-SHA256 (100K iterations).

## Configuration

The API reads `appsettings.json` for:
- `ConnectionStrings:DefaultConnection` — SQLite path (default: `loreloom.db`)
- `Groq:ApiKey` — Groq API key (required for LLM calls)
- `Groq:Model` — LLM model (default: `llama-3.3-70b-versatile`)
- `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` — JWT signing configuration

The web client uses the API base URL from its host (same origin). User preferences (language) are stored in `localStorage`.

## Conventions

### Project structure
- **Models** (`Core/Models/`) — Mutable EF Core entity classes with navigation properties.
- **DTOs** (`Core/Dtos/`) — Immutable C# `record` types, suffixed `Request`/`Response`. Validated with data annotations.
- **Engine** (`Core/Engine/`) — Static classes (`ContextBuilder`, `ResourceTracker`) plus scoped `TurnManager`.
- **Services** (`Core/Services/`) — `ILlmService` interface with `GroqLlmService` implementation.
- **Pages** (`Web/Pages/`) — Blazor pages, one per route. Use MudBlazor components throughout.
- **Services** (`Web/Services/`) — `AuthService` (auth state + localStorage) and `LoreLoomApiClient` (typed HTTP client).

### Code style
- C# 12 primary constructors in controllers and services.
- `async`/`await` throughout; methods suffixed with `Async`.
- EF Core Fluent API in `OnModelCreating` for schema constraints.
- MudBlazor dark theme with purple/gold palette. All UI built with Mud components.
- All LLM responses are forced JSON mode; deserialized with `snake_case` naming policy.
- Tokens are internal — never shown to the user in the web UI. Joining games, sending turns, etc. are done by clicking buttons; the JWT provides identity.

### Multi-language
- LLM responses generated in the player's chosen language (8 supported: en, ca, es, fr, de, it, pt, ja).
- Language preference stored in `localStorage` and passed when creating/starting games.
