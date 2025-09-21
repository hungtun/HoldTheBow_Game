using Hobow_Server.Models;
using Hobow_Server.Services;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Hubs;
using SharedLibrary.Responses;
using SharedLibrary.Events;
using Hobow_Server.Physics;

namespace Hobow_Server.Handlers;

public interface IEnemyHandler
{
    Task InitializeEnemiesAsync();
    int SpawnEnemyAtPoint(EnemySpawnPoint spawnPoint, EnemyDefinition enemyDef);
    Task<int> SpawnEnemyAsync(string mapId, string enemyName, float x, float y);
    IEnumerable<EnemyState> GetAllEnemies();
    EnemyMoveUpdateEvent ProcessEnemyMoveIntent(EnemyMoveIntentEvent moveIntent);
    Task UpdateEnemyAIAsync();
    Task<bool> DamageEnemyAsync(int enemyId, int damage);
    Task RespawnEnemiesAsync();
}

public class EnemyHandler : IEnemyHandler
{
    private readonly IEnemyService _enemyService;
    private readonly GameState _gameState;
    private readonly IHubContext<EnemyHub> _enemyHub;
    private readonly ILogger<EnemyHandler> _logger;
    private readonly ServerPhysicsManager _physics;
    
    // Optimization: track previous positions to avoid sending unchanged data
    private readonly Dictionary<int, (float X, float Y)> _lastEnemyPositions = new();

    public EnemyHandler(IEnemyService enemyService, GameState gameState, IHubContext<EnemyHub> enemyHub, ILogger<EnemyHandler> logger, ServerPhysicsManager physics)
    {
        _enemyService = enemyService;
        _gameState = gameState;
        _enemyHub = enemyHub;
        _logger = logger;
        _physics = physics;
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

        // Create physics body for enemy
        _physics.CreateEnemyBody(enemyId, new Microsoft.Xna.Framework.Vector2(x, y), 0.25f);

        _logger.LogInformation($"[EnemyHandler] Spawned {enemyDef.EnemyName} at ({x:F1}, {y:F1})");

        return enemyId;
    }

    public async Task<int> SpawnEnemyAtPointAsync(EnemySpawnPoint spawnPoint, EnemyDefinition enemyDef)
    {
        var enemyId = SpawnEnemyAtPoint(spawnPoint, enemyDef);

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
        
        // Initialize tracking data for optimization
        _lastEnemyPositions[enemyId] = (x, y);

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


    /// <summary>
    /// Process enemy movement intent event and return position update event
    /// </summary>
    public EnemyMoveUpdateEvent ProcessEnemyMoveIntent(EnemyMoveIntentEvent moveIntent)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var enemy = _gameState.GetEnemy(moveIntent.EnemyId);
            if (enemy == null)
            {
                _logger.LogWarning($"[EnemyHandler] Enemy {moveIntent.EnemyId} not found in ProcessEnemyMoveIntent");
                return null;
            }

            // Check if new position is valid (no collision with map, other enemies, or heroes)
            var newPosition = new Microsoft.Xna.Framework.Vector2(moveIntent.TargetX, moveIntent.TargetY);
            if (!_physics.IsPositionValid(newPosition, 0.25f, moveIntent.EnemyId))
            {
                _logger.LogDebug($"[EnemyHandler] Enemy {moveIntent.EnemyId} position ({moveIntent.TargetX:F2}, {moveIntent.TargetY:F2}) is invalid, keeping current position");
                
                // Return current position if move is invalid
                return new EnemyMoveUpdateEvent
                {
                    EnemyId = enemy.EnemyId,
                    EnemyName = enemy.EnemyName,
                    MapId = enemy.MapId,
                    X = enemy.X,
                    Y = enemy.Y,
                    ServerTimestampMs = serverTimestamp,
                    IsMoving = false,
                    MovementType = "AI"
                };
            }

            // Update enemy position in game state
            _gameState.UpdateEnemyPosition(moveIntent.EnemyId, moveIntent.TargetX, moveIntent.TargetY);
            
            // Update physics body position
            _physics.SetEnemyPosition(moveIntent.EnemyId, newPosition);

            return new EnemyMoveUpdateEvent
            {
                EnemyId = enemy.EnemyId,
                EnemyName = enemy.EnemyName,
                MapId = enemy.MapId,
                X = moveIntent.TargetX,
                Y = moveIntent.TargetY,
                ServerTimestampMs = serverTimestamp,
                IsMoving = true,
                MovementType = "AI"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHandler] Error in ProcessEnemyMoveIntent for enemy {EnemyId}", moveIntent.EnemyId);
            return null;
        }
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
                var attackRadius = 1f;

                if (distance <= chaseRadius && distance > attackRadius)
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

                        // Check if position changed significantly (optimization)
                        var lastPos = _lastEnemyPositions.GetValueOrDefault(enemy.EnemyId, (enemy.X, enemy.Y));
                        var distanceMoved = Math.Sqrt(Math.Pow(newX - lastPos.X, 2) + Math.Pow(newY - lastPos.Y, 2));
                        
                        // Only send update if enemy moved more than 0.1 units (optimization threshold)
                        if (distanceMoved > 0.1f)
                        {
                            // Use event-based approach for enemy movement
                            var moveIntent = new EnemyMoveIntentEvent
                            {
                                EnemyId = enemy.EnemyId,
                                TargetX = newX,
                                TargetY = newY,
                                MoveSpeed = moveSpeed,
                                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            };

                            var updateEvent = ProcessEnemyMoveIntent(moveIntent);
                            if (updateEvent != null)
                            {
                                await _enemyHub.Clients.All.SendAsync("EnemyMoveUpdate", updateEvent);
                                
                                // Update last position
                                _lastEnemyPositions[enemy.EnemyId] = (newX, newY);
                            }
                        }
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

            await BroadcastEnemyDamagedAsync(enemyId, damage, enemy.Health);

            if (enemy.Health <= 0)
            {
                _gameState.RemoveEnemy(enemyId);
                _physics.RemoveEnemyBody(enemyId); // Remove physics body
                _lastEnemyPositions.Remove(enemyId); // Clean up tracking data
                await BroadcastEnemyRemovedAsync(enemyId);

                _logger.LogInformation($"[EnemyHandler] Enemy {enemyId} died and was removed");
                return true;
            }

            return false;
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

    /// <summary>
    /// Respawn all disabled enemies (set IsActive = true and reset health)
    /// </summary>
    public Task RespawnEnemiesAsync()
    {
        try
        {
            // Get all enemies including disabled ones
            var allEnemies = _gameState.GetAllEnemiesIncludingDisabled();
            var disabledEnemies = allEnemies.Where(e => !e.IsActive).ToList();
            
            foreach (var enemy in disabledEnemies)
            {
                // Reset enemy state
                enemy.IsActive = true;
                enemy.Health = 100; // Reset to full health
                enemy.ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                _logger.LogInformation($"[EnemyHandler] Respawned enemy {enemy.EnemyId}");
            }
            
            if (disabledEnemies.Any())
            {
                _logger.LogInformation($"[EnemyHandler] Respawned {disabledEnemies.Count} enemies");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHandler] Failed to respawn enemies");
        }
        
        return Task.CompletedTask;
    }
}