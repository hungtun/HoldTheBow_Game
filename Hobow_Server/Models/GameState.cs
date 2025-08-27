using System.Collections.Concurrent;

namespace Hobow_Server.Models;

public class GameState
{
    private readonly ConcurrentDictionary<int, HeroState> _heroes = new();
    
    public HeroState GetHero(int heroId)
    {
        _heroes.TryGetValue(heroId, out var hero);
        return hero;
    }
    
    public void AddHero(int heroId, float x = 0f, float y = 0f)
    {
        Console.WriteLine($"[GameState] Adding hero with ID: {heroId} at position ({x}, {y})");
        var heroState = new HeroState
        {
            Id = heroId,
            X = x,  
            Y = y, 
            LastMoveTime = 0 
        };
        _heroes.TryAdd(heroId, heroState);
        Console.WriteLine($"[GameState] Added hero {heroId} at position ({heroState.X}, {heroState.Y})");
    }
    
    public void UpdateHero(HeroState hero)
    {
        _heroes.AddOrUpdate(hero.Id, hero, (key, oldValue) => hero);
    }
    
    public void RemoveHero(int heroId)
    {
        _heroes.TryRemove(heroId, out _);
    }
    
    public IEnumerable<HeroState> GetAllHeroes()
    {
        return _heroes.Values;
    }
}

public class HeroState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public long LastMoveTime { get; set; }
}
