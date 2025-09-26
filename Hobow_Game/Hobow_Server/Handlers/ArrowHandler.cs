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
    private readonly Hobow_Server.Services.IHeroService _heroService;

    private const float MAX_CHARGE_TIME = 1.0f;
    private const float MAX_DAMAGE = 50f;
    private const float BASE_DAMAGE = 10f;
    private const float ARROW_SPEED = 15f;
    private const float ARROW_MAX_LIFETIME = 3f;
    private const float ARROW_SIZE = 0.1f;

    public ArrowHandler(GameState gameState, IHubContext<HeroHub> heroHub, IHubContext<EnemyHub> enemyHub, ILogger<ArrowHandler> logger, ServerPhysicsManager physics, Hobow_Server.Services.IHeroService heroService)
    {
        _gameState = gameState;
        _heroHub = heroHub;
        _enemyHub = enemyHub;
        _logger = logger;
        _physics = physics;
        _heroService = heroService;
    }


    public ArrowSpawnedEvent ProcessArrowShootIntent(ArrowShootIntentEvent shootIntent, string sourceId)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var hero = _gameState.GetHero(shootIntent.HeroId);
            if (hero == null)
            {

                return null;
            }

            var chargePercent = Math.Clamp(shootIntent.ChargePercent, 0f, 1f);

            int heroMaxDamage = (int)MAX_DAMAGE;
            try
            {
                var heroEntityTask = _heroService.GetHeroAsync(shootIntent.HeroId);
                heroEntityTask.Wait();
                var heroEntity = heroEntityTask.Result;
                if (heroEntity != null && heroEntity.Damage > 0)
                {
                    heroMaxDamage = heroEntity.Damage;
                }
            }
            catch (Exception fetchEx)
            {

            }

            var damage = BASE_DAMAGE + (heroMaxDamage - BASE_DAMAGE) * chargePercent;

            var accuracy = chargePercent;

            var directionX = shootIntent.DirectionX;
            var directionY = shootIntent.DirectionY;

            if (accuracy < 1f)
            {
                var inaccuracy = (1f - accuracy) * 0.3f;
                var randomAngle = (_random.NextSingle() - 0.5f) * inaccuracy;

                var cos = Math.Cos(randomAngle);
                var sin = Math.Sin(randomAngle);
                var newX = directionX * cos - directionY * sin;
                var newY = directionX * sin + directionY * cos;

                directionX = (float)newX;
                directionY = (float)newY;
            }

            var length = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (length > 0)
            {
                directionX /= (float)length;
                directionY /= (float)length;
            }

            var arrowId = _gameState.SpawnArrow(shootIntent.HeroId, shootIntent.StartX, shootIntent.StartY,
                directionX, directionY, ARROW_SPEED, damage, accuracy);


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


    public async Task UpdateArrowsAsync()
    {
        try
        {
            var arrows = _gameState.GetAllArrows().Where(a => a.IsActive && !a.IsStuck).ToList();

            foreach (var arrow in arrows)
            {
                var ageSeconds = (DateTime.UtcNow - arrow.CreatedAt).TotalSeconds;
                if (ageSeconds >= ARROW_MAX_LIFETIME)
                {
                    _gameState.RemoveArrow(arrow.ArrowId);
                    var removeEvent = new ArrowRemovedEvent
                    {
                        ArrowId = arrow.ArrowId,
                        ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await _heroHub.Clients.All.SendAsync("ArrowRemoved", removeEvent);
                    _logger.LogInformation($"[ArrowHandler] Removed expired arrow {arrow.ArrowId} after {ageSeconds:F2}s");
                    continue;
                }

                var deltaTime = 0.016f;
                var newX = arrow.X + arrow.DirectionX * arrow.Speed * deltaTime;
                var newY = arrow.Y + arrow.DirectionY * arrow.Speed * deltaTime;

                var hitResult = CheckArrowCollision(arrow, newX, newY);

                if (hitResult.HasHit)
                {
                    await HandleArrowHit(arrow, hitResult);
                }
                else
                {
                    _gameState.UpdateArrowPosition(arrow.ArrowId, newX, newY);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ArrowHandler] Error in UpdateArrowsAsync");
        }
    }

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
        if (!_physics.IsPositionValid(new Microsoft.Xna.Framework.Vector2(newX, newY), ARROW_SIZE, arrow.ArrowId))
        {
            return (true, "Wall", null, newX, newY);
        }

        var enemies = _gameState.GetAllEnemies();
        foreach (var enemy in enemies)
        {
            if (enemy.HitboxHalfSizeX > 0f && enemy.HitboxHalfSizeY > 0f)
            {
                float cx = enemy.X + enemy.HitboxCenterOffsetX;
                float cy = enemy.Y + enemy.HitboxCenterOffsetY;
                float minX = cx - enemy.HitboxHalfSizeX;
                float maxX = cx + enemy.HitboxHalfSizeX;
                float minY = cy - enemy.HitboxHalfSizeY;
                float maxY = cy + enemy.HitboxHalfSizeY;

                if (newX >= minX && newX <= maxX && newY >= minY && newY <= maxY)
                {
                    return (true, "Enemy", enemy.EnemyId, newX, newY);
                }
            }
            else
            {
                const float enemyCenterOffsetY = 0.30f;
                const float enemyHitRadius = 0.60f;
                var enemyCenterY = enemy.Y + enemyCenterOffsetY;
                var dxE = newX - enemy.X;
                var dyE = newY - enemyCenterY;
                var distance = Math.Sqrt(dxE * dxE + dyE * dyE);
                if (distance < enemyHitRadius)
                {
                    return (true, "Enemy", enemy.EnemyId, newX, newY);
                }
            }
        }

        var heroes = _gameState.GetAllHeroes();
        foreach (var hero in heroes)
        {
            if (hero.Id == arrow.HeroId) continue; 

            if (hero.HitboxHalfSizeX > 0f && hero.HitboxHalfSizeY > 0f)
            {
                float cx = hero.X + hero.HitboxCenterOffsetX;
                float cy = hero.Y + hero.HitboxCenterOffsetY;
                float minX = cx - hero.HitboxHalfSizeX;
                float maxX = cx + hero.HitboxHalfSizeX;
                float minY = cy - hero.HitboxHalfSizeY;
                float maxY = cy + hero.HitboxHalfSizeY;

                if (newX >= minX && newX <= maxX && newY >= minY && newY <= maxY)
                {
                    return (true, "Hero", hero.Id, newX, newY);
                }
            }
            else
            {
                var heroCenterY = hero.Y + hero.ProbeOffsetY;
                var dxH = newX - hero.X;
                var dyH = newY - heroCenterY;
                var distance = Math.Sqrt(dxH * dxH + dyH * dyH);
                const float heroHitRadius = 0.50f;
                if (distance < heroHitRadius)
                {
                    return (true, "Hero", hero.Id, newX, newY);
                }
            }
        }

        return (false, "", null, 0, 0);
    }

    private async Task HandleArrowHit(Arrow arrow, (bool HasHit, string HitType, int? TargetId, float HitX, float HitY) hitResult)
    {
        try
        {
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

            if (hitResult.HitType == "Enemy" && hitResult.TargetId.HasValue)
            {
                var enemy = _gameState.GetEnemy(hitResult.TargetId.Value);
                if (enemy != null)
                {
                    enemy.Health -= (int)arrow.Damage;
                    hitEvent.RemainingHealth = enemy.Health;


                    if (enemy.Health <= 0)
                    {
                        enemy.IsActive = false;

                        await _enemyHub.Clients.All.SendAsync("EnemyRemoved", hitResult.TargetId.Value);
                    }
                }
            }
            else if (hitResult.HitType == "Hero" && hitResult.TargetId.HasValue)
            {
                var hero = _gameState.GetHero(hitResult.TargetId.Value);
                if (hero != null)
                {
                    hitEvent.RemainingHealth = 100; 
                }
            }

            if (hitResult.HitType == "Enemy")
            {
                await _enemyHub.Clients.All.SendAsync("ArrowHit", hitEvent);
            }
            else
            {
                await _heroHub.Clients.All.SendAsync("ArrowHit", hitEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ArrowHandler] Error in HandleArrowHit for arrow {ArrowId}", arrow.ArrowId);
        }
    }
}
