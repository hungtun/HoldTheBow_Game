using System.Collections.Concurrent;
using System.Threading;
using SharedLibrary.DataModels;

namespace Hobow_Server.Models;

public class GameState
{
    private readonly ConcurrentDictionary<int, HeroState> _heroes = new();
    private readonly ConcurrentDictionary<int, EnemyState> _enemies = new();
    private readonly ConcurrentDictionary<int, Arrow> _arrows = new();
    private int _nextEnemyId = 0;
    private int _nextArrowId = 0;

    public GameState()
    {
    }

    #region ==== Hero Management ====
    public HeroState? GetHero(int heroId)
    {
        _heroes.TryGetValue(heroId, out var hero);
        return hero;
    }

    public void AddHero(int heroId, float x = 0f, float y = 0f, string mapId = "Home")
    {
        Console.WriteLine($"[GameState] Adding hero {heroId} at ({x}, {y}) on map {mapId}");
        var heroState = new HeroState
        {
            Id = heroId,
            X = x,
            Y = y,
            LastMoveTime = 0,
            MapId = mapId
        };
        _heroes.TryAdd(heroId, heroState);
    }

    public void UpdateHero(HeroState hero)
    {
        _heroes.AddOrUpdate(hero.Id, hero, (key, old) => hero);
    }

    public void RemoveHero(int heroId)
    {
        _heroes.TryRemove(heroId, out _);
    }

    public IEnumerable<HeroState> GetAllHeroes() => _heroes.Values;

    public HeroState? GetUserActiveHero(int userId)
    {

        return null;
    }

    public void RemoveUserActiveHero(int userId)
    {

    }


    #endregion

    #region ==== Enemy Management ====

    public int SpawnEnemy(string mapId, string enemyName, float x, float y)
    {
        var id = Interlocked.Increment(ref _nextEnemyId);

        var enemyState = new EnemyState
        {
            EnemyId = id,
            EnemyName = enemyName,
            MapId = mapId,
            X = x,
            Y = y,
            Health = 100,
            Attack = 10,
            MoveSpeed = 2f,
            ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _enemies.TryAdd(id, enemyState);

        Console.WriteLine($"[GameState] Spawned enemy {enemyState.EnemyName} ({enemyState.EnemyId}) at ({x},{y})");

        return id;
    }

    public void UpdateEnemyPosition(int enemyId, float x, float y)
    {
        if (_enemies.TryGetValue(enemyId, out var enemy))
        {
            enemy.X = x;
            enemy.Y = y;
            enemy.ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public EnemyState? GetEnemy(int enemyId)
    {
        _enemies.TryGetValue(enemyId, out var enemy);
        return enemy;
    }

    public IEnumerable<EnemyState> GetAllEnemies() => _enemies.Values.Where(e => e.IsActive);
    
    public IEnumerable<EnemyState> GetAllEnemiesIncludingDisabled() => _enemies.Values;

    public void UpdateEnemyHealth(int enemyId, int newHealth)
    {
        if (_enemies.TryGetValue(enemyId, out var enemy))
        {
            enemy.Health = newHealth;
            enemy.ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public void RemoveEnemy(int enemyId)
    {
        if (_enemies.TryGetValue(enemyId, out var enemy))
        {
            enemy.IsActive = false;
        }
    }

    public void ClearAllEnemies()
    {
        _enemies.Clear();
        _nextEnemyId = 0;
    }

    #endregion

    #region ==== Arrow Management ====

    public int SpawnArrow(int heroId, float x, float y, float directionX, float directionY, float speed, float damage, float accuracy)
    {
        var id = Interlocked.Increment(ref _nextArrowId);

        var arrow = new Arrow
        {
            ArrowId = id,
            HeroId = heroId,
            X = x,
            Y = y,
            DirectionX = directionX,
            DirectionY = directionY,
            Speed = speed,
            Damage = damage,
            Accuracy = accuracy,
            IsActive = true,
            IsStuck = false,
            CreatedAt = DateTime.UtcNow
        };

        _arrows.TryAdd(id, arrow);

        Console.WriteLine($"[GameState] Spawned arrow {id} by hero {heroId} at ({x},{y})");

        return id;
    }

    public void UpdateArrowPosition(int arrowId, float x, float y)
    {
        if (_arrows.TryGetValue(arrowId, out var arrow))
        {
            arrow.X = x;
            arrow.Y = y;
        }
    }

    public void StickArrow(int arrowId, string targetType, int? targetId, float x, float y)
    {
        if (_arrows.TryGetValue(arrowId, out var arrow))
        {
            arrow.X = x;
            arrow.Y = y;
            arrow.IsStuck = true;
            arrow.StuckTargetType = targetType;
            arrow.StuckTargetId = targetId;
            arrow.RemoveAt = DateTime.UtcNow.AddSeconds(5); // Remove after 5 seconds
        }
    }

    public Arrow? GetArrow(int arrowId)
    {
        _arrows.TryGetValue(arrowId, out var arrow);
        return arrow;
    }

    public IEnumerable<Arrow> GetAllArrows() => _arrows.Values;

    public void RemoveArrow(int arrowId)
    {
        _arrows.TryRemove(arrowId, out _);
    }

    #endregion
}

public class HeroState
{
    public int Id { get; set; }
    public string Name { get; set; } = "Hero";
    public float X { get; set; }
    public float Y { get; set; }
    public long LastMoveTime { get; set; }
    public string MapId { get; set; } = "Home";
    public float HeroRadius { get; set; } = 0.25f;
    public float ProbeOffsetY { get; set; } = -0.3f;
}

public class EnemyState
{
    public int EnemyId { get; set; }
    public string EnemyName { get; set; } = "Enemy";
    public string MapId { get; set; } = "Home";
    public float X { get; set; }
    public float Y { get; set; }
    public int Health { get; set; }
    public int Attack { get; set; }
    public float MoveSpeed { get; set; }
    public long ServerTimestampMs { get; set; }
    public bool IsActive { get; set; } = true;
}