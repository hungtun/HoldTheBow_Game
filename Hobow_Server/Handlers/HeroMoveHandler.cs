using SharedLibrary.Requests;
using SharedLibrary.Responses;
using Hobow_Server.Models;

namespace Hobow_Server.Handlers;

public interface IHeroMoveHandler
{
    HeroMoveResponse ProcessMove(HeroMoveRequest request, string sourceId);
    IEnumerable<HeroState> GetAllHeroes();
    void RemoveHero(int heroId);
}

public class HeroMoveHandler : IHeroMoveHandler
{
    private readonly GameState _gameState;

    public HeroMoveHandler(GameState gameState)
    {
        _gameState = gameState;
    }

    public HeroMoveResponse ProcessMove(HeroMoveRequest request, string sourceId)
    {
        try
        {
            var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_gameState == null)
            {
                return null;
            }

            var hero = _gameState.GetHero(request.HeroId);
            // Kiểm tra nếu hero có vị trí bất thường (quá xa), reset về (0,0)
            if (Math.Abs(hero.X) > 1000f || Math.Abs(hero.Y) > 1000f)
            {
                _gameState.RemoveHero(request.HeroId);
                _gameState.AddHero(request.HeroId, 0f, 0f);
                hero = _gameState.GetHero(request.HeroId);
            }

            // Tính toán delta time từ lần di chuyển cuối
            float deltaTime;
            if (hero.LastMoveTime == 0)
            {
                // Hero mới được tạo, sử dụng delta time nhỏ
                deltaTime = 0.016f; // ~60 FPS
            }
            else
            {
                deltaTime = (serverTimestamp - hero.LastMoveTime) / 1000f;
                if (deltaTime > 0.1f) // Max 100ms
                {
                    deltaTime = 0.1f;
                }
            }
            hero.LastMoveTime = serverTimestamp;

            // Tính toán vị trí mới dựa trên direction và speed
            float moveDistance = request.Speed * deltaTime;

            switch (request.Direction.ToLower())
            {
                case "left": hero.X -= moveDistance; break;
                case "right": hero.X += moveDistance; break;
                case "up": hero.Y += moveDistance; break;
                case "down": hero.Y -= moveDistance; break;
            }

            _gameState.UpdateHero(hero);
            Console.WriteLine($"[HeroMoveHandler] Player {request.HeroId} final position: ({hero.X}, {hero.Y})");

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
    
    public IEnumerable<HeroState> GetAllHeroes()
    {
        return _gameState.GetAllHeroes();
    }

    public void RemoveHero(int heroId)
    {
        _gameState.RemoveHero(heroId);
        Console.WriteLine($"[HeroMoveHandler] Removed hero {heroId} from game state");
    }
}

