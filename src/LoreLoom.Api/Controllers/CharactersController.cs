using LoreLoom.Api.Extensions;
using LoreLoom.Core.Data;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Localization;
using LoreLoom.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class CharactersController(LoreLoomDbContext db, IAppTextLocalizer text) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterResponse>> Create(CreateCharacterRequest request)
    {
        var totalStats = request.Strength + request.Wit + request.Charisma;
        if (totalStats != 9)
            return BadRequest(text["api_stats_must_sum"]);

        var playerToken = this.GetAccountToken() ?? request.PlayerToken;

        var character = new Character
        {
            Id = Guid.NewGuid(),
            PlayerToken = playerToken,
            Name = request.Name,
            Backstory = request.Backstory,
            Strength = request.Strength,
            Wit = request.Wit,
            Charisma = request.Charisma
        };

        db.Characters.Add(character);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByToken), new { token = character.PlayerToken }, ToResponse(character));
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<CharacterResponse>>> GetMine()
    {
        var accountToken = this.GetAccountToken();
        if (string.IsNullOrEmpty(accountToken))
            return Unauthorized();

        var characters = await db.Characters
            .Where(c => c.PlayerToken == accountToken)
            .OrderByDescending(c => c.LastPlayedAt)
            .ToListAsync();

        return characters.Select(ToResponse).ToList();
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<List<CharacterResponse>>> GetByToken(string token)
    {
        var characters = await db.Characters
            .Where(c => c.PlayerToken == token)
            .OrderByDescending(c => c.LastPlayedAt)
            .ToListAsync();

        return characters.Select(ToResponse).ToList();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CharacterResponse>> Update(Guid id, UpdateCharacterRequest request)
    {
        var character = await db.Characters.FindAsync(id);
        if (character is null)
            return NotFound();

        if (request.Strength.HasValue) character.Strength = request.Strength.Value;
        if (request.Wit.HasValue) character.Wit = request.Wit.Value;
        if (request.Charisma.HasValue) character.Charisma = request.Charisma.Value;

        await db.SaveChangesAsync();

        return ToResponse(character);
    }

    [HttpPost("{id:guid}/add-xp")]
    public async Task<ActionResult<CharacterResponse>> AddXp(Guid id, [FromQuery] int xp)
    {
        var character = await db.Characters.FindAsync(id);
        if (character is null)
            return NotFound();

        character.Xp += xp;

        while (character.Xp >= character.Level * 100)
        {
            character.Xp -= character.Level * 100;
            character.Level++;
        }

        character.LastPlayedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToResponse(character);
    }

    private static CharacterResponse ToResponse(Character c) => new(
        c.Id,
        c.Name,
        c.Backstory,
        c.Strength,
        c.Wit,
        c.Charisma,
        c.Level,
        c.Xp,
        XpToNextLevel: c.Level * 100 - c.Xp,
        c.CreatedAt,
        c.LastPlayedAt
    );
}
