using Hobow_Server.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace Hobow_Server.Services;

public interface IHeroService
{
    // Data Access Methods
    Task<Hero?> GetHeroAsync(int heroId);
    Task<List<Hero>> GetUserHeroesAsync(int userId);
    Task<Hero> CreateHeroAsync(CreateHeroRequest request, int userId);
    Task UpdateHeroAsync(Hero hero);
    Task DeleteHeroAsync(int heroId);
    Task<bool> HeroExistsAsync(int heroId);

    // Business Logic Methods
    Task<HeroResponse> GetHeroResponseAsync(int heroId);
    Task<List<HeroResponse>> GetUserHeroesResponseAsync(int userId);
    Task<HeroResponse> CreateHeroResponseAsync(CreateHeroRequest request, int userId);
    Task<HeroResponse?> SelectHeroAsync(int heroId, int userId, string connectionId);
    Task<HeroResponse?> GetActiveHeroResponseAsync(int userId);
    Task<bool> DeselectHeroAsync(int userId);
}

public class HeroService : IHeroService
{
    private readonly GameDbContext _context;
    private readonly ILogger<HeroService> _logger;

    public HeroService(GameDbContext context, ILogger<HeroService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Data Access Methods
    public async Task<Hero?> GetHeroAsync(int heroId)
    {
        try
        {
            return await _context.Heroes
                .Include(h => h.User)
                .FirstOrDefaultAsync(h => h.Id == heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroService] Failed to get hero {HeroId}", heroId);
            return null;
        }
    }

    public async Task<List<Hero>> GetUserHeroesAsync(int userId)
    {
        try
        {
            return await _context.Heroes
                .Include(h => h.User)
                .Where(h => h.User.Id == userId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroService] Failed to get heroes for user {UserId}", userId);
            return new List<Hero>();
        }
    }

    public async Task<Hero> CreateHeroAsync(CreateHeroRequest request, int userId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Heroes)
                .FirstAsync(u => u.Id == userId);

            var hero = new Hero
            {
                Name = request.Name,
                User = user,
                Level = 1
                // Bỏ Experience vì model Hero không có property này
            };

            _context.Heroes.Add(hero);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[HeroService] Created hero {HeroName} for user {UserId}", hero.Name, userId);
            return hero;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroService] Failed to create hero for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateHeroAsync(Hero hero)
    {
        try
        {
            _context.Heroes.Update(hero);
            await _context.SaveChangesAsync();
            _logger.LogInformation("[HeroService] Updated hero {HeroId}", hero.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroService] Failed to update hero {HeroId}", hero.Id);
            throw;
        }
    }

    public async Task DeleteHeroAsync(int heroId)
    {
        try
        {
            var hero = await _context.Heroes.FindAsync(heroId);
            if (hero != null)
            {
                _context.Heroes.Remove(hero);
                await _context.SaveChangesAsync();
                _logger.LogInformation("[HeroService] Deleted hero {HeroId}", heroId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroService] Failed to delete hero {HeroId}", heroId);
            throw;
        }
    }

    public async Task<bool> HeroExistsAsync(int heroId)
    {
        try
        {
            return await _context.Heroes.AnyAsync(h => h.Id == heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroService] Failed to check if hero exists {HeroId}", heroId);
            return false;
        }
    }

    public async Task<HeroResponse> GetHeroResponseAsync(int heroId)
    {
        var hero = await GetHeroAsync(heroId);
        if (hero == null)
            throw new ArgumentException($"Hero with ID {heroId} not found");

        return new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        };
    }

    public async Task<List<HeroResponse>> GetUserHeroesResponseAsync(int userId)
    {
        var heroes = await GetUserHeroesAsync(userId);
        return heroes.Select(hero => new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        }).ToList();
    }

    public async Task<HeroResponse> CreateHeroResponseAsync(CreateHeroRequest request, int userId)
    {
        var hero = await CreateHeroAsync(request, userId);
        return new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        };
    }

    public async Task<HeroResponse?> SelectHeroAsync(int heroId, int userId, string connectionId)
    {
        var hero = await GetHeroAsync(heroId);
        if (hero == null)
            return null;

        if (hero.User.Id != userId)
            throw new UnauthorizedAccessException("You don't have permission to select this hero");

        // Không còn quản lý user connection ở đây nữa
        // Logic này sẽ được xử lý ở PlayerSessionHandler
        _logger.LogInformation("[HeroService] User {UserId} selected hero {HeroId} ({HeroName}) with connection {ConnectionId}", userId, hero.Id, hero.Name, connectionId);

        return new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        };
    }

    public async Task<HeroResponse?> GetActiveHeroResponseAsync(int userId)
    {
        // Không còn quản lý active hero ở đây nữa
        // Logic này sẽ được xử lý ở PlayerSessionHandler
        _logger.LogInformation("[HeroService] GetActiveHeroResponseAsync called for user {UserId}", userId);
        return null;
    }

    public Task<bool> DeselectHeroAsync(int userId)
    {
        // Không còn quản lý active hero ở đây nữa
        // Logic này sẽ được xử lý ở PlayerSessionHandler
        _logger.LogInformation("[HeroService] DeselectHeroAsync called for user {UserId}", userId);
        return Task.FromResult(true);
    }
}
