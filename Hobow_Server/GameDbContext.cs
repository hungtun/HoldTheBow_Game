using Microsoft.EntityFrameworkCore;
using SharedLibrary;

namespace Hobow_Server;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Hero> Heroes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Hero>().ToTable("Hero");
        modelBuilder.Entity<User>().ToTable("Users");
    }
}
