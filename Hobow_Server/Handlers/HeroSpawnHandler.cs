using SharedLibrary.Requests;
using SharedLibrary.Responses;
using Hobow_Server.Models;

namespace Hobow_Server.Handlers;

public interface IHeroSpawnHandler
{
    HeroSpawnResponse ProcessSpawn(HeroSpawnRequest request, string sourceId);
}

public class HeroSpawnHandler : IHeroSpawnHandler
{
    private readonly GameState _gameState;

    public HeroSpawnHandler(GameState gameState)
    {
        _gameState = gameState;
    }

    public HeroSpawnResponse ProcessSpawn(HeroSpawnRequest request, string sourceId)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_gameState == null)
            {
                return null;
            }

            // Kiểm tra xem hero đã tồn tại chưa
            var hero = _gameState.GetHero(request.HeroId);
            
            if (hero == null)
            {
                // Tạo hero mới nếu chưa tồn tại với vị trí spawn
                _gameState.AddHero(request.HeroId, request.X, request.Y);
                hero = _gameState.GetHero(request.HeroId);
            }

            // Cập nhật vị trí spawn
            hero.X = request.X;
            hero.Y = request.Y;
            hero.LastMoveTime = serverTimestamp;

            // Cập nhật GameState
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
}
