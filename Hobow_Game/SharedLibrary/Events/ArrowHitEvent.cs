namespace SharedLibrary.Events;

/// <summary>
/// Event sent from server to all clients when an arrow hits something
/// </summary>
public class ArrowHitEvent
{
    public int ArrowId { get; set; }
    public int HeroId { get; set; }
    public float HitX { get; set; }
    public float HitY { get; set; }
    public string HitType { get; set; } = ""; // "Wall", "Enemy", "Hero"
    public int? TargetId { get; set; } // EnemyId or HeroId if hit enemy/hero
    public float Damage { get; set; }
    public int? RemainingHealth { get; set; } // For enemies/heroes
    public bool IsStuck { get; set; } // Whether arrow is stuck in target
    public long ServerTimestampMs { get; set; }
}
