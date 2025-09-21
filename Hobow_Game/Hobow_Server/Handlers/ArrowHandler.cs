using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Hubs;
using Hobow_Server.Models;
using Hobow_Server.Physics;
using SharedLibrary.Events;
using SharedLibrary.DataModels;
using System.Numerics;

namespace Hobow_Server.Handlers;

public interface IArrowHandler
{
    ArrowSpawnedEvent ProcessArrowShootIntent(ArrowShootIntentEvent shootIntent, string sourceId);
    Task UpdateArrowsAsync();
    Task RemoveStuckArrowsAsync();
}

public class ArrowHandler : IArrowHandler
{
    private readonly GameState _gameState;
    private readonly IHubContext<HeroHub> _heroHub;
    private readonly IHubContext<EnemyHub> _enemyHub;
    private readonly ILogger<ArrowHandler> _logger;
    private readonly ServerPhysicsManager _physics;
    private readonly Random _random = new();
    
    // Arrow constants
    private const float MAX_CHARGE_TIME = 1.0f; // 1 second max charge
    private const float MAX_DAMAGE = 50f;
    private const float BASE_DAMAGE = 10f;
    private const float ARROW_SPEED = 15f;
    private const float ARROW_LIFETIME = 5f; // 5 seconds stuck lifetime
    private const float ARROW_SIZE = 0.1f; // For collision detection

    public ArrowHandler(GameState gameState, IHubContext<HeroHub> heroHub, IHubContext<EnemyHub> enemyHub, ILogger<ArrowHandler> logger, ServerPhysicsManager physics)
    {
        _gameState = gameState;
        _heroHub = heroHub;
        _enemyHub = enemyHub;
        _logger = logger;
        _physics = physics;
    }

    /// <summary>
    /// Process arrow shoot intent and spawn arrow if valid
    /// </summary>
    public ArrowSpawnedEvent ProcessArrowShootIntent(ArrowShootIntentEvent shootIntent, string sourceId)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Console.WriteLine($"[ArrowHandler] Processing shoot intent - HeroId: {shootIntent.HeroId}, Start: ({shootIntent.StartX}, {shootIntent.StartY}), Direction: ({shootIntent.DirectionX}, {shootIntent.DirectionY}), Charge: {shootIntent.ChargeTime}");

            // Validate hero exists
            var hero = _gameState.GetHero(shootIntent.HeroId);
            if (hero == null)
            {
                _logger.LogWarning($"[ArrowHandler] Hero {shootIntent.HeroId} not found in ProcessArrowShootIntent");
                return null;
            }

            // Calculate damage based on charge (10-50 damage)
            var chargePercent = Math.Clamp(shootIntent.ChargePercent, 0f, 1f);
            var damage = BASE_DAMAGE + (MAX_DAMAGE - BASE_DAMAGE) * chargePercent;

            // Calculate accuracy based on charge (more charge = more accurate)
            var accuracy = chargePercent;

            // Add some randomness to direction based on accuracy
            var directionX = shootIntent.DirectionX;
            var directionY = shootIntent.DirectionY;
            
            if (accuracy < 1f)
            {
                var inaccuracy = (1f - accuracy) * 0.3f; // Max 30% inaccuracy
                var randomAngle = (_random.NextSingle() - 0.5f) * inaccuracy;
                
                // Rotate direction vector by random angle
                var cos = Math.Cos(randomAngle);
                var sin = Math.Sin(randomAngle);
                var newX = directionX * cos - directionY * sin;
                var newY = directionX * sin + directionY * cos;
                
                directionX = (float)newX;
                directionY = (float)newY;
            }

            // Normalize direction
            var length = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (length > 0)
            {
                directionX /= (float)length;
                directionY /= (float)length;
            }

            // Create arrow
            var arrowId = _gameState.SpawnArrow(shootIntent.HeroId, shootIntent.StartX, shootIntent.StartY, 
                directionX, directionY, ARROW_SPEED, damage, accuracy);

            _logger.LogInformation($"[ArrowHandler] Arrow {arrowId} shot by hero {shootIntent.HeroId}, damage: {damage:F1}, accuracy: {accuracy:F2}");

            return new ArrowSpawnedEvent
            {
                ArrowId = arrowId,
                HeroId = shootIntent.HeroId,
                StartX = shootIntent.StartX,
                StartY = shootIntent.StartY,
                DirectionX = directionX,
                DirectionY = directionY,
                Speed = ARROW_SPEED,
                Damage = damage,
                Accuracy = accuracy,
                ServerTimestampMs = serverTimestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ArrowHandler] Error in ProcessArrowShootIntent for hero {HeroId}", shootIntent.HeroId);
            return null;
        }
    }

    /// <summary>
    /// Update all active arrows (move them and check collisions)
    /// </summary>
    public async Task UpdateArrowsAsync()
    {
        try
        {
            var arrows = _gameState.GetAllArrows().Where(a => a.IsActive && !a.IsStuck).ToList();
            
            foreach (var arrow in arrows)
            {
                // Move arrow
                var deltaTime = 0.016f; // ~60 FPS
                var newX = arrow.X + arrow.DirectionX * arrow.Speed * deltaTime;
                var newY = arrow.Y + arrow.DirectionY * arrow.Speed * deltaTime;

                // Check for collisions
                var hitResult = CheckArrowCollision(arrow, newX, newY);
                
                if (hitResult.HasHit)
                {
                    // Arrow hit something
                    await HandleArrowHit(arrow, hitResult);
                }
                else
                {
                    // Update arrow position
                    _gameState.UpdateArrowPosition(arrow.ArrowId, newX, newY);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ArrowHandler] Error in UpdateArrowsAsync");
        }
    }

    /// <summary>
    /// Remove arrows that have been stuck for 5 seconds
    /// </summary>
    public async Task RemoveStuckArrowsAsync()
    {
        try
        {
            var stuckArrows = _gameState.GetAllArrows()
                .Where(a => a.IsStuck && a.RemoveAt.HasValue && DateTime.UtcNow >= a.RemoveAt.Value)
                .ToList();

            foreach (var arrow in stuckArrows)
            {
                _gameState.RemoveArrow(arrow.ArrowId);
                
                var removeEvent = new ArrowRemovedEvent
                {
                    ArrowId = arrow.ArrowId,
                    ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _heroHub.Clients.All.SendAsync("ArrowRemoved", removeEvent);
                
                _logger.LogInformation($"[ArrowHandler] Removed stuck arrow {arrow.ArrowId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ArrowHandler] Error in RemoveStuckArrowsAsync");
        }
    }

    private (bool HasHit, string HitType, int? TargetId, float HitX, float HitY) CheckArrowCollision(Arrow arrow, float newX, float newY)
    {
        // Check wall collision
        if (!_physics.IsPositionValid(new Microsoft.Xna.Framework.Vector2(newX, newY), ARROW_SIZE, arrow.ArrowId))
        {
            Console.WriteLine($"[ArrowHandler] Arrow {arrow.ArrowId} hit wall at ({newX}, {newY})");
            return (true, "Wall", null, newX, newY);
        }

        // Check enemy collision
        var enemies = _gameState.GetAllEnemies();
        Console.WriteLine($"[ArrowHandler] Checking collision for arrow {arrow.ArrowId} at ({newX}, {newY}) against {enemies.Count()} enemies");
        
        foreach (var enemy in enemies)
        {
            var distance = Math.Sqrt(Math.Pow(newX - enemy.X, 2) + Math.Pow(newY - enemy.Y, 2));
            Console.WriteLine($"[ArrowHandler] Arrow {arrow.ArrowId} distance to enemy {enemy.EnemyId} at ({enemy.X}, {enemy.Y}): {distance:F2}");
            
            if (distance < 0.5f) // Enemy hit radius
            {
                Console.WriteLine($"[ArrowHandler] Arrow {arrow.ArrowId} HIT enemy {enemy.EnemyId}!");
                return (true, "Enemy", enemy.EnemyId, newX, newY);
            }
        }

        // Check hero collision (other heroes, not the shooter)
        var heroes = _gameState.GetAllHeroes();
        foreach (var hero in heroes)
        {
            if (hero.Id == arrow.HeroId) continue; // Don't hit the shooter
            
            var distance = Math.Sqrt(Math.Pow(newX - hero.X, 2) + Math.Pow(newY - hero.Y, 2));
            if (distance < 0.5f) // Hero hit radius
            {
                Console.WriteLine($"[ArrowHandler] Arrow {arrow.ArrowId} hit hero {hero.Id}!");
                return (true, "Hero", hero.Id, newX, newY);
            }
        }

        return (false, "", null, 0, 0);
    }

    private async Task HandleArrowHit(Arrow arrow, (bool HasHit, string HitType, int? TargetId, float HitX, float HitY) hitResult)
    {
        try
        {
            // Mark arrow as stuck
            _gameState.StickArrow(arrow.ArrowId, hitResult.HitType, hitResult.TargetId, hitResult.HitX, hitResult.HitY);
            
            var hitEvent = new ArrowHitEvent
            {
                ArrowId = arrow.ArrowId,
                HeroId = arrow.HeroId,
                HitX = hitResult.HitX,
                HitY = hitResult.HitY,
                HitType = hitResult.HitType,
                TargetId = hitResult.TargetId,
                Damage = arrow.Damage,
                IsStuck = true,
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Handle damage if hit enemy or hero
            if (hitResult.HitType == "Enemy" && hitResult.TargetId.HasValue)
            {
                var enemy = _gameState.GetEnemy(hitResult.TargetId.Value);
                if (enemy != null)
                {
                    enemy.Health -= (int)arrow.Damage;
                    hitEvent.RemainingHealth = enemy.Health;
                    
                    _logger.LogInformation($"[ArrowHandler] Arrow {arrow.ArrowId} hit enemy {hitResult.TargetId}, damage: {arrow.Damage}, remaining health: {enemy.Health}");
                    
                    if (enemy.Health <= 0)
                    {
                        // Disable enemy instead of removing from DB
                        enemy.IsActive = false;
                        _logger.LogInformation($"[ArrowHandler] Enemy {hitResult.TargetId} killed by arrow {arrow.ArrowId} - disabled");
                        
                        // Send enemy death event to all clients via enemy hub
                        await _enemyHub.Clients.All.SendAsync("EnemyRemoved", hitResult.TargetId.Value);
                    }
                }
            }
            else if (hitResult.HitType == "Hero" && hitResult.TargetId.HasValue)
            {
                var hero = _gameState.GetHero(hitResult.TargetId.Value);
                if (hero != null)
                {
                    // For now, heroes don't take damage (can be implemented later)
                    hitEvent.RemainingHealth = 100; // Full health for heroes
                    _logger.LogInformation($"[ArrowHandler] Arrow {arrow.ArrowId} hit hero {hitResult.TargetId} (no damage)");
                }
            }

            // Send hit event to all clients via appropriate hub
            if (hitResult.HitType == "Enemy")
            {
                Console.WriteLine($"[ArrowHandler] Sending ArrowHit event to EnemyHub - ArrowId: {hitEvent.ArrowId}, TargetId: {hitEvent.TargetId}, HitType: {hitEvent.HitType}");
                await _enemyHub.Clients.All.SendAsync("ArrowHit", hitEvent);
            }
            else
            {
                Console.WriteLine($"[ArrowHandler] Sending ArrowHit event to HeroHub - ArrowId: {hitEvent.ArrowId}, TargetId: {hitEvent.TargetId}, HitType: {hitEvent.HitType}");
                await _heroHub.Clients.All.SendAsync("ArrowHit", hitEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ArrowHandler] Error in HandleArrowHit for arrow {ArrowId}", arrow.ArrowId);
        }
    }
}
