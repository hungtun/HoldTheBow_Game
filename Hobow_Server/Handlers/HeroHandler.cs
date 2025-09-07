using SharedLibrary.Requests;
using SharedLibrary.Responses;
using Hobow_Server.Models;
using Hobow_Server.Physics;
using Microsoft.Xna.Framework;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Hubs;
using Hobow_Server.Services;


namespace Hobow_Server.Handlers;

public interface IHeroHandler
{
    // Hero Spawn Methods
    HeroSpawnResponse ProcessSpawn(HeroSpawnRequest request, string sourceId);

    // Hero Move Methods
    HeroMoveResponse ProcessMove(HeroMoveRequest request, string sourceId);
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
    // Không cần GameSessionManager nữa - sử dụng ActiveSessionId trong database

    public HeroHandler(GameState gameState, ServerPhysicsManager physics, IHubContext<HeroHub> heroHub, ILogger<HeroHandler> logger, IHeroService heroService)
    {
        _gameState = gameState;
        _physics = physics;
        _heroHub = heroHub;
        _logger = logger;
        _heroService = heroService;
    }

    #region ==== Hero Spawn Methods ====

    public HeroSpawnResponse ProcessSpawn(HeroSpawnRequest request, string sourceId)
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
            }

            float spawnX = request.X;
            float spawnY = request.Y;

            hero.X = spawnX;
            hero.Y = spawnY;
            hero.LastMoveTime = serverTimestamp;
            hero.MapId = string.IsNullOrWhiteSpace(request.MapId) ? hero.MapId : request.MapId;

            hero.HeroRadius = request.HeroRadius > 0 ? request.HeroRadius : 0.25f;
            hero.ProbeOffsetY = request.ProbeOffsetY;

            _physics.CreateHeroBody(request.HeroId, new Microsoft.Xna.Framework.Vector2(spawnX, spawnY), hero.HeroRadius);

            _gameState.UpdateHero(hero);

            return new HeroSpawnResponse
            {
                HeroId = hero.Id,
                X = hero.X,
                Y = hero.Y,
                ServerTimestampMs = serverTimestamp
            };
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    #endregion

    #region ==== Hero Move Methods ====

    public HeroMoveResponse ProcessMove(HeroMoveRequest request, string sourceId)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var hero = _gameState.GetHero(request.HeroId);
            if (hero == null)
            {
                _gameState.AddHero(request.HeroId, 0, 0, "Home");
                hero = _gameState.GetHero(request.HeroId);
            }

            float moveDistance = request.Speed * 0.05f;
            float targetX = hero.X;
            float targetY = hero.Y;
            switch (request.Direction.ToLower())
            {
                case "left": targetX -= moveDistance; break;
                case "right": targetX += moveDistance; break;
                case "up": targetY += moveDistance; break;
                case "down": targetY -= moveDistance; break;
            }

            var currentPosition = new Microsoft.Xna.Framework.Vector2(hero.X, hero.Y);
            var targetPosition = new Microsoft.Xna.Framework.Vector2(targetX, targetY);

            if (!_physics.HasHeroBody(request.HeroId))
            {
                _physics.CreateHeroBody(request.HeroId, currentPosition, hero.HeroRadius);
            }

            if (!_physics.IsPositionValid(targetPosition, hero.HeroRadius))
            {
                targetX = hero.X;
                targetY = hero.Y;
            }
            else
            {
                _physics.SetHeroPosition(request.HeroId, targetPosition);
            }

            if (Math.Abs(targetX - hero.X) < 0.001f && Math.Abs(targetY - hero.Y) < 0.001f)
            {
                return new HeroMoveResponse
                {
                    HeroId = hero.Id,
                    X = hero.X,
                    Y = hero.Y,
                    ServerTimestampMs = serverTimestamp
                };
            }

            hero.X = targetX;
            hero.Y = targetY;

            _gameState.UpdateHero(hero);

            return new HeroMoveResponse
            {
                HeroId = hero.Id,
                X = hero.X,
                Y = hero.Y,
                ServerTimestampMs = serverTimestamp
            };
        }
        catch (Exception ex)
        {
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
