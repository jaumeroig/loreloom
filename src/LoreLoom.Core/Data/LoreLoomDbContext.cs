using LoreLoom.Core.Enums;
using LoreLoom.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Core.Data;

public class LoreLoomDbContext : DbContext
{
    public LoreLoomDbContext(DbContextOptions<LoreLoomDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Turn> Turns => Set<Turn>();
    public DbSet<GameResult> GameResults => Set<GameResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Username).IsRequired().HasMaxLength(50);
            e.Property(a => a.PasswordHash).IsRequired();
            e.Property(a => a.Token).IsRequired().HasMaxLength(100);
            e.HasIndex(a => a.Username).IsUnique();
            e.HasIndex(a => a.Token).IsUnique();
        });

        modelBuilder.Entity<Character>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.PlayerToken).IsRequired().HasMaxLength(200);
            e.Property(c => c.Strength).HasDefaultValue(1);
            e.Property(c => c.Wit).HasDefaultValue(1);
            e.Property(c => c.Charisma).HasDefaultValue(1);
            e.Property(c => c.Level).HasDefaultValue(1);
            e.Property(c => c.Xp).HasDefaultValue(0);
            e.HasIndex(c => c.PlayerToken);
        });

        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Title).IsRequired().HasMaxLength(200);
            e.Property(g => g.Setting).IsRequired();
            e.Property(g => g.ResourceName).IsRequired().HasMaxLength(200);
            e.Property(g => g.ResourcePct).HasDefaultValue(100);
            e.Property(g => g.Status)
                .HasConversion<string>()
                .HasDefaultValue(GameStatus.Waiting);
            e.Property(g => g.InviteCode).HasMaxLength(20);
            e.Property(g => g.MaxPlayers).HasDefaultValue(4);
            e.HasIndex(g => g.Status);
            e.HasIndex(g => g.InviteCode).IsUnique()
                .HasFilter("\"InviteCode\" IS NOT NULL");
        });

        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(100);
            e.Property(p => p.Token).IsRequired().HasMaxLength(200);
            e.HasOne(p => p.Game).WithMany(g => g.Players).HasForeignKey(p => p.GameId);
            e.HasOne(p => p.Character).WithMany(c => c.Players).HasForeignKey(p => p.CharacterId);
            e.HasIndex(p => new { p.GameId, p.Token }).IsUnique();
        });

        modelBuilder.Entity<Turn>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.PlayerAction).IsRequired();
            e.Property(t => t.DmResponse).IsRequired();
            e.HasOne(t => t.Game).WithMany(g => g.Turns).HasForeignKey(t => t.GameId);
            e.HasOne(t => t.Player).WithMany(p => p.Turns).HasForeignKey(t => t.PlayerId);
            e.HasIndex(t => t.GameId);
        });

        modelBuilder.Entity<GameResult>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Game).WithMany(g => g.Results).HasForeignKey(r => r.GameId);
            e.HasOne(r => r.Player).WithMany(p => p.GameResults).HasForeignKey(r => r.PlayerId);
            e.HasOne(r => r.Character).WithMany(c => c.GameResults).HasForeignKey(r => r.CharacterId);
            e.HasIndex(r => r.GameId);
        });
    }
}
