using Microsoft.EntityFrameworkCore;
using Hobow_Server.Models;

namespace Hobow_Server;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Hero> Heroes { get; set; }
    public DbSet<EnemyDefinition> EnemyDefinitions { get; set; }
    public DbSet<EnemySpawnPoint> EnemySpawnPoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Hero>().ToTable("Hero");
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<EnemyDefinition>().ToTable("EnemyDefinitions");
        modelBuilder.Entity<EnemySpawnPoint>().ToTable("EnemySpawnPoints");
    }
}
