using SharedLibrary.Requests;
using SharedLibrary.Responses;
using SharedLibrary.Events;
using Hobow_Server.Models;
using Hobow_Server.Physics;
using Microsoft.Xna.Framework;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Hubs;
using Hobow_Server.Services;


namespace Hobow_Server.Handlers;

public interface IHeroHandler
{
    HeroSpawnResponse ProcessSpawn(HeroSpawnRequest request);
    HeroMoveUpdateEvent ProcessMoveIntent(HeroMoveIntentEvent moveIntent);
    BowStateUpdateEvent ProcessBowStateIntent(BowStateIntentEvent bowStateIntent);
    IEnumerable<HeroState> GetAllHeroes();
    void RemoveHero(int heroId);
    Task RemoveUserActiveHeroAsync(int userId);
}

public class HeroHandler : IHeroHandler
{
    private readonly GameState _gameState;
    private readonly ServerPhysicsManager _physics;
    private readonly IHubContext<HeroHub> _heroHub;
    private readonly ILogger<HeroHandler> _logger;
    private readonly IHeroService _heroService;

    public HeroHandler(GameState gameState, ServerPhysicsManager physics, IHubContext<HeroHub> heroHub, ILogger<HeroHandler> logger, IHeroService heroService)
    {
        _gameState = gameState;
        _physics = physics;
        _heroHub = heroHub;
        _logger = logger;
        _heroService = heroService;
    }

    #region ==== Hero Spawn Methods ====

    public HeroSpawnResponse ProcessSpawn(HeroSpawnRequest request)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_gameState == null)
            {
                return null;
            }

            var hero = _gameState.GetHero(request.HeroId);
            if (hero == null)
            {
                _gameState.AddHero(request.HeroId, request.X, request.Y, request.MapId);
                hero = _gameState.GetHero(request.HeroId);
                if (hero == null)
                {
                    return null;
                }
            }

            float spawnX = request.X;
            float spawnY = request.Y;

            hero.X = spawnX;
            hero.Y = spawnY;
            hero.LastMoveTime = serverTimestamp;
            hero.MapId = string.IsNullOrWhiteSpace(request.MapId) ? hero.MapId : request.MapId;

            hero.HeroRadius = request.HeroRadius > 0 ? request.HeroRadius : 0.25f;
            hero.ProbeOffsetY = request.ProbeOffsetY;
            hero.MaxHealth = Math.Max(hero.MaxHealth, 1);
            if (hero.CurrentHealth <= 0 || hero.CurrentHealth > hero.MaxHealth)
            {
                hero.CurrentHealth = hero.MaxHealth;
            }
            hero.HitboxCenterOffsetX = request.HitboxCenterOffsetX;
            hero.HitboxCenterOffsetY = request.HitboxCenterOffsetY;
            hero.HitboxHalfSizeX = request.HitboxHalfSizeX;
            hero.HitboxHalfSizeY = request.HitboxHalfSizeY;

            _physics.CreateHeroBody(request.HeroId, new Vector2(spawnX, spawnY), hero.HeroRadius);

            _gameState.UpdateHero(hero);

            int maxHp = 100; int damage = 50;
            try
            {
                var dbHeroTask = _heroService.GetHeroAsync(request.HeroId);
                dbHeroTask.Wait();
                var dbHero = dbHeroTask.Result;
                if (dbHero != null) { maxHp = dbHero.MaxHealth; damage = dbHero.Damage; }
            }
            catch { }

            return new HeroSpawnResponse
            {
                HeroId = hero.Id,
                X = hero.X,
                Y = hero.Y,
                MaxHealth = maxHp,
                Damage = damage,
                ServerTimestampMs = serverTimestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHandler] Error in ProcessSpawn for hero {HeroId}", request.HeroId);
            return null;
        }
    }

    #endregion

    #region ==== Hero Move Methods ====

    public HeroMoveUpdateEvent ProcessMoveIntent(HeroMoveIntentEvent moveIntent)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var hero = _gameState.GetHero(moveIntent.HeroId);
            if (hero == null)
            {
                _gameState.AddHero(moveIntent.HeroId, 0, 0, "Home");
                hero = _gameState.GetHero(moveIntent.HeroId);
                if (hero == null)
                {
                    _logger.LogError($"[HeroHandler] Failed to auto-create hero {moveIntent.HeroId} in ProcessMoveIntent");
                    return null;
                }
            }

            float moveDistance = moveIntent.Speed * 0.05f;
            float targetX = hero.X;
            float targetY = hero.Y;
            bool isMoving = !string.IsNullOrEmpty(moveIntent.Direction) && moveIntent.Speed > 0;

            if (isMoving)
            {
                switch (moveIntent.Direction.ToLower())
                {
                    case "left": targetX -= moveDistance; break;
                    case "right": targetX += moveDistance; break;
                    case "up": targetY += moveDistance; break;
                    case "down": targetY -= moveDistance; break;
                }
            }

            var currentPosition = new Vector2(hero.X, hero.Y);
            var targetPosition = new Vector2(targetX, targetY);

            if (!_physics.HasHeroBody(moveIntent.HeroId))
            {
                _physics.CreateHeroBody(moveIntent.HeroId, currentPosition, hero.HeroRadius);
            }

            if (isMoving && !_physics.IsPositionValid(targetPosition, hero.HeroRadius))
            {
                targetX = hero.X;
                targetY = hero.Y;
                isMoving = false;
            }
            else if (isMoving)
            {
                _physics.SetHeroPosition(moveIntent.HeroId, targetPosition);
            }

            // Update hero position if it changed
            bool positionChanged = Math.Abs(targetX - hero.X) > 0.001f || Math.Abs(targetY - hero.Y) > 0.001f;
            if (positionChanged)
            {
                hero.X = targetX;
                hero.Y = targetY;
                _gameState.UpdateHero(hero);
            }

            return new HeroMoveUpdateEvent
            {
                HeroId = hero.Id,
                X = hero.X,
                Y = hero.Y,
                ServerTimestampMs = serverTimestamp,
                Direction = moveIntent.Direction,
                IsMoving = isMoving
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHandler] Error in ProcessMoveIntent for hero {HeroId}", moveIntent.HeroId);
            return null;
        }
    }

    public BowStateUpdateEvent ProcessBowStateIntent(BowStateIntentEvent bowStateIntent)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var hero = _gameState.GetHero(bowStateIntent.HeroId);
            if (hero == null)
            {
                _logger.LogWarning($"[HeroHandler] Hero {bowStateIntent.HeroId} not found in ProcessBowStateIntent");
                return null;
            }

            // Validate bow state data
            var validatedAngle = Math.Clamp(bowStateIntent.AngleDeg, -180f, 180f);
            var validatedChargePercent = Math.Clamp(bowStateIntent.ChargePercent, 0f, 1f);

            return new BowStateUpdateEvent
            {
                HeroId = bowStateIntent.HeroId,
                AngleDeg = validatedAngle,
                IsCharging = bowStateIntent.IsCharging,
                ChargePercent = validatedChargePercent,
                ServerTimestampMs = serverTimestamp,
                Action = bowStateIntent.IsCharging ? "Update" : "Stop"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHandler] Error in ProcessBowStateIntent for hero {HeroId}", bowStateIntent.HeroId);
            return null;
        }
    }

    public async Task<HeroResponse?> SelectHeroAsync(int heroId, int userId, string connectionId)
    {
        try
        {
            await RemoveUserActiveHeroAsync(userId);
            
            _gameState.AddHero(heroId, 0, 0, "Home");

            var hero = _gameState.GetHero(heroId);
            if (hero == null)
            {
                throw new InvalidOperationException("Failed to create hero");
            }

            var response = new HeroSpawnResponse
            {
                HeroId = hero.Id,
                X = hero.X,
                Y = hero.Y,
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await _heroHub.Clients.All.SendAsync("HeroSpawned", response);
            
            
            return new HeroResponse
            {
                Id = hero.Id,
                Name = hero.Name,
                UserId = userId,
                Username = "User" + userId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHandler] Failed to select hero {HeroId} for user {UserId}", heroId, userId);
            throw;
        }
    }

    public IEnumerable<HeroState> GetAllHeroes()
    {
        return _gameState.GetAllHeroes();
    }

    public void RemoveHero(int heroId)
    {
        _gameState.RemoveHero(heroId);
    }

    public async Task RemoveUserActiveHeroAsync(int userId)
    {
        try
        {
            var userHeroes = await _heroService.GetUserHeroesAsync(userId);
            
            var activeHero = _gameState.GetAllHeroes()
                .FirstOrDefault(h => userHeroes.Any(uh => uh.Id == h.Id));
            
            if (activeHero != null)
            {
                _logger.LogInformation("[HeroHandler] Removing active hero {HeroId} for user {UserId}", activeHero.Id, userId);
                
                await _heroHub.Clients.All.SendAsync("HeroRemoved", activeHero.Id);
                
                _gameState.RemoveHero(activeHero.Id);
                _physics.RemoveHeroBody(activeHero.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHandler] Failed to remove active hero for user {UserId}", userId);
        }
    }

    #endregion
}
