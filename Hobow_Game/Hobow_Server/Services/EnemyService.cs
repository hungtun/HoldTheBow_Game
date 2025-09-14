using Hobow_Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Hobow_Server.Services;

public interface IEnemyService
{
    Task<List<EnemySpawnPoint>> GetEnabledSpawnPointsAsync();
    Task<EnemyDefinition?> GetEnemyDefinitionAsync(int enemyDefinitionId);
    Task<List<EnemyDefinition>> GetAllEnemyDefinitionsAsync();
}

public class EnemyService : IEnemyService
{
    private readonly GameDbContext _context;
    private readonly ILogger<EnemyService> _logger;

    public EnemyService(GameDbContext context, ILogger<EnemyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<EnemySpawnPoint>> GetEnabledSpawnPointsAsync()
    {
        try
        {
            return await _context.EnemySpawnPoints
                .Where(sp => sp.IsEnabled)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyService] Failed to get enabled spawn points");
            return new List<EnemySpawnPoint>();
        }
    }

    public async Task<EnemyDefinition?> GetEnemyDefinitionAsync(int enemyDefinitionId)
    {
        try
        {
            return await _context.EnemyDefinitions
                .FirstOrDefaultAsync(ed => ed.Id == enemyDefinitionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyService] Failed to get enemy definition {EnemyDefinitionId}", enemyDefinitionId);
            return null;
        }
    }

    public async Task<List<EnemyDefinition>> GetAllEnemyDefinitionsAsync()
    {
        try
        {
            return await _context.EnemyDefinitions.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyService] Failed to get all enemy definitions");
            return new List<EnemyDefinition>();
        }
    }

}
