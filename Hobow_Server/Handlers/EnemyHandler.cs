using Hobow_Server.Models;
using Hobow_Server.Services;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Hubs;
using SharedLibrary.Responses;

namespace Hobow_Server.Handlers;

public interface IEnemyHandler
{
    Task InitializeEnemiesAsync();
    int SpawnEnemyAtPoint(EnemySpawnPoint spawnPoint, EnemyDefinition enemyDef);
    Task<int> SpawnEnemyAsync(string mapId, string enemyName, float x, float y);
    IEnumerable<EnemyState> GetAllEnemies();
    Task UpdateEnemyPositionAsync(int enemyId, float x, float y);
    Task UpdateEnemyAIAsync();
    Task<bool> DamageEnemyAsync(int enemyId, int damage);
}

public class EnemyHandler : IEnemyHandler
{
    private readonly IEnemyService _enemyService;
    private readonly GameState _gameState;
    private readonly IHubContext<EnemyHub> _enemyHub;
    private readonly ILogger<EnemyHandler> _logger;

    public EnemyHandler(IEnemyService enemyService, GameState gameState, IHubContext<EnemyHub> enemyHub, ILogger<EnemyHandler> logger)
    {
        _enemyService = enemyService;
        _gameState = gameState;
        _enemyHub = enemyHub;
        _logger = logger;
    }

    public async Task InitializeEnemiesAsync()
    {
        try
        {
            _logger.LogInformation("[EnemyHandler] Starting enemy initialization...");

            await Task.Delay(2000);

            var spawnPoints = await _enemyService.GetEnabledSpawnPointsAsync();
            _logger.LogInformation($"[EnemyHandler] Found {spawnPoints.Count} enabled spawn points");

            foreach (var spawnPoint in spawnPoints)
            {
                var enemyDef = await _enemyService.GetEnemyDefinitionAsync(spawnPoint.EnemyDefinitionId);
                if (enemyDef == null)
                {
                    _logger.LogWarning($"[EnemyHandler] Enemy definition not found for spawn point {spawnPoint.Id}");
                    continue;
                }

                for (int i = 0; i < spawnPoint.MaxAlive; i++)
                {
                    var enemyId = await SpawnEnemyAtPointAsync(spawnPoint, enemyDef);

                    await Task.Delay(500);
                }
            }

            var totalSpawned = spawnPoints.Sum(sp => sp.MaxAlive);

            _gameState.GetAllEnemies().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHandler] Auto spawn failed");
        }
    }

    public int SpawnEnemyAtPoint(EnemySpawnPoint spawnPoint, EnemyDefinition enemyDef)
    {
        var random = new Random();
        var angle = random.NextDouble() * 2 * Math.PI;
        var distance = random.NextDouble() * spawnPoint.SpawnRadius;

        var x = spawnPoint.CenterX + (float)(Math.Cos(angle) * distance);
        var y = spawnPoint.CenterY + (float)(Math.Sin(angle) * distance);

        var enemyId = _gameState.SpawnEnemy(spawnPoint.MapId, enemyDef.EnemyName, x, y);

        _logger.LogInformation($"[EnemyHandler] Spawned {enemyDef.EnemyName} at ({x:F1}, {y:F1})");

        return enemyId;
    }

    public async Task<int> SpawnEnemyAtPointAsync(EnemySpawnPoint spawnPoint, EnemyDefinition enemyDef)
    {
        var enemyId = SpawnEnemyAtPoint(spawnPoint, enemyDef);

        // Broadcast qua SignalR
        var response = new EnemySpawnResponse
        {
            EnemyId = enemyId,
            EnemyName = enemyDef.EnemyName,
            MapId = spawnPoint.MapId,
            X = _gameState.GetEnemy(enemyId)?.X ?? 0,
            Y = _gameState.GetEnemy(enemyId)?.Y ?? 0
        };

        await _enemyHub.Clients.All.SendAsync("EnemySpawned", response);

        return enemyId;
    }

    public async Task<int> SpawnEnemyAsync(string mapId, string enemyName, float x, float y)
    {
        if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(enemyName))
        {
            throw new ArgumentException("MapId and EnemyName cannot be null or empty");
        }

        if (x < 0 || y < 0)
        {
            throw new ArgumentException("Position coordinates must be positive");
        }

        var enemyId = _gameState.SpawnEnemy(mapId, enemyName, x, y);

        // Broadcast qua SignalR
        var response = new EnemySpawnResponse
        {
            EnemyId = enemyId,
            EnemyName = enemyName,
            MapId = mapId,
            X = x,
            Y = y
        };

        await _enemyHub.Clients.All.SendAsync("EnemySpawned", response);

        _logger.LogInformation($"[EnemyHandler] Spawned {enemyName} at ({x}, {y}) on map {mapId}");

        return enemyId;
    }

    public IEnumerable<EnemyState> GetAllEnemies()
    {
        return _gameState.GetAllEnemies();
    }

    public async Task UpdateEnemyPositionAsync(int enemyId, float x, float y)
    {
        _gameState.UpdateEnemyPosition(enemyId, x, y);

        var enemy = _gameState.GetEnemy(enemyId);
        if (enemy == null)
        {
            _logger.LogWarning($"[EnemyHandler] Enemy {enemyId} not found");
            return;
        }

        var response = new EnemySpawnResponse
        {
            EnemyId = enemyId,
            EnemyName = enemy.EnemyName,
            MapId = enemy.MapId,
            X = x,
            Y = y
        };

        await _enemyHub.Clients.All.SendAsync("EnemyMoved", response);

    }

    public async Task UpdateEnemyAIAsync()
    {
        try
        {
            var enemies = _gameState.GetAllEnemies().ToList();
            var heroes = _gameState.GetAllHeroes().ToList();

            if (!heroes.Any()) return;

            foreach (var enemy in enemies)
            {
                var nearestHero = heroes
                    .OrderBy(h => Math.Sqrt(Math.Pow(h.X - enemy.X, 2) + Math.Pow(h.Y - enemy.Y, 2)))
                    .FirstOrDefault();

                if (nearestHero == null) continue;

                var distance = Math.Sqrt(Math.Pow(nearestHero.X - enemy.X, 2) + Math.Pow(nearestHero.Y - enemy.Y, 2));
                var chaseRadius = 5f;

                if (distance <= chaseRadius && distance > 1f)
                {
                    var directionX = nearestHero.X - enemy.X;
                    var directionY = nearestHero.Y - enemy.Y;
                    var length = Math.Sqrt(directionX * directionX + directionY * directionY);

                    if (length > 0)
                    {
                        directionX /= (float)length;
                        directionY /= (float)length;

                        var moveSpeed = enemy.MoveSpeed * 0.05f;
                        var newX = enemy.X + directionX * moveSpeed;
                        var newY = enemy.Y + directionY * moveSpeed;

                        await UpdateEnemyPositionAsync(enemy.EnemyId, newX, newY);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHandler] Failed to update enemy AI");
        }
    }

    public async Task<bool> DamageEnemyAsync(int enemyId, int damage)
    {
        try
        {
            var enemy = _gameState.GetEnemy(enemyId);
            if (enemy == null) return false;

            enemy.Health -= damage;

            _logger.LogInformation($"[EnemyHandler] Enemy {enemyId} took {damage} damage, health: {enemy.Health}");

            // Broadcast damage
            await BroadcastEnemyDamagedAsync(enemyId, damage, enemy.Health);

            // Kiểm tra enemy chết
            if (enemy.Health <= 0)
            {
                _gameState.RemoveEnemy(enemyId);
                await BroadcastEnemyRemovedAsync(enemyId);

                _logger.LogInformation($"[EnemyHandler] Enemy {enemyId} died and was removed");
                return true; // Enemy died
            }

            return false; // Enemy still alive
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHandler] Failed to damage enemy {EnemyId}", enemyId);
            return false;
        }
    }


    private async Task BroadcastEnemyDamagedAsync(int enemyId, int damage, int newHealth)
    {
        await _enemyHub.Clients.All.SendAsync("EnemyDamaged", new
        {
            EnemyId = enemyId,
            Damage = damage,
            NewHealth = newHealth
        });
    }

    private async Task BroadcastEnemyRemovedAsync(int enemyId)
    {
        await _enemyHub.Clients.All.SendAsync("EnemyRemoved", enemyId);
    }
}