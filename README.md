# LoreLoom

LoreLoom is a multiplayer, turn-based tabletop RPG where an LLM acts as the Dungeon Master.

Players create characters, join a shared adventure, and describe their actions in natural language. The backend turns those actions into structured prompts for Groq, interprets the model response as narrative + game state updates, and advances the session for the next player.

The current client is a **Blazor WebAssembly** app built with **MudBlazor**, hosted by the same **ASP.NET Core API** on a single Azure App Service.

## Features

- Email-based registration and login
- Character creation and progression
- Public and private multiplayer games
- Lobby flow and active turn-based play
- Shared party resource instead of classic HP
- Global ranking
- Markdown export of finished adventures
- Multi-language play experience

## Solution structure

The solution (`LoreLoom.slnx`) contains three projects:

- `src/LoreLoom.Core` — shared domain models, DTOs, enums, EF Core data layer, game engine, and LLM integration
- `src/LoreLoom.Api` — ASP.NET Core REST API, JWT auth, database migrations, and static hosting for the web client
- `src/LoreLoom.Web` — Blazor WebAssembly frontend built with MudBlazor

## Architecture overview

### Backend

`LoreLoom.Api` owns:

- REST endpoints for auth, characters, games, turns, results, and ranking
- JWT authentication
- EF Core + SQLite persistence
- Groq integration through `GroqLlmService`
- Hosting the published Blazor WASM app with:
  - `UseBlazorFrameworkFiles()`
  - `UseStaticFiles()`
  - `MapFallbackToFile("index.html")`

### Frontend

`LoreLoom.Web` is a Blazor WebAssembly app that:

- uses MudBlazor for the UI
- authenticates with JWT
- stores session information in `localStorage`
- talks to the API through `LoreLoomApiClient`
- hides internal tokens from the user entirely

Joining a public game is done through the UI directly; users never need to copy or paste gameplay tokens.

### Game loop

1. A player sends an action.
2. The API validates turn ownership and builds the LLM context.
3. Groq returns structured narrative and outcome data.
4. The game resource is updated.
5. XP, victory/defeat, and turn order are resolved.
6. The next player continues from the updated state.

## Tech stack

- .NET 10
- ASP.NET Core
- Blazor WebAssembly
- MudBlazor
- Entity Framework Core
- SQLite
- Groq API
- Azure App Service

## Getting started

### Prerequisites

- .NET 10 SDK
- A Groq API key

### Configuration

The main configuration lives in `src/LoreLoom.Api/appsettings.json`.

Important settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=loreloom.db"
  },
  "Groq": {
    "ApiKey": "",
    "Model": "llama-3.3-70b-versatile"
  },
  "Jwt": {
    "Key": "change-this-in-production",
    "Issuer": "LoreLoom",
    "Audience": "LoreLoom"
  }
}
```

Notes:

- Set `Groq:ApiKey` before running the app for real gameplay.
- Replace the default `Jwt:Key` in any non-local environment.
- SQLite is used by default for simplicity.

### Run locally

Build everything:

```bash
dotnet build
```

Run the hosted app:

```bash
dotnet run --project src/LoreLoom.Api
```

Then open the local URL shown by ASP.NET Core. The API and web client are served from the same host.

## Database migrations

Migrations live in `src/LoreLoom.Core/Data/Migrations`.

Add a new migration from the repository root:

```bash
dotnet ef migrations add <MigrationName> --project src/LoreLoom.Core --startup-project src/LoreLoom.Api
```

The API applies migrations automatically on startup.

## Authentication model

- Users register with **email + username + password**
- Login returns a JWT
- The JWT contains the account identity plus the internal account token used by the game model
- The frontend sends the JWT as a Bearer token

This keeps the web UX simple while preserving the existing domain model internally.

## Deployment

Deployment is handled by `.github/workflows/deploy-api.yml`.

The workflow publishes:

```bash
dotnet publish src/LoreLoom.Api/LoreLoom.Api.csproj -c Release -o ./publish
```

Because the API references the web project, the publish output contains both:

- the ASP.NET Core API
- the Blazor WebAssembly client

That single artifact is deployed to the `loreloom-api` Azure App Service.

## Current status

The original CLI client has been removed. LoreLoom now ships as a web-first application served by the API.

## Limitations

- There is currently no automated test project in the repository.
- Real-time updates still rely on polling; SignalR could be a future improvement.
- SQLite is convenient for early stages, but may need to be revisited for larger-scale production usage.

## Future improvements

- Email invitations for private games
- Password reset flows
- Real-time updates with SignalR
- Broader production hardening and observability

