using LoreLoom.Core.Data;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class CharactersController(LoreLoomDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterResponse>> Create(CreateCharacterRequest request)
    {
        var totalStats = request.Strength + request.Wit + request.Charisma;
        if (totalStats != 9)
            return BadRequest("Stats must sum to 9 at creation (distribute 9 points across Strength, Wit, Charisma with each between 1-5).");

        var character = new Character
        {
            Id = Guid.NewGuid(),
            PlayerToken = request.PlayerToken,
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
