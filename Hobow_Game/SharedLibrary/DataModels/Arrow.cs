namespace SharedLibrary.DataModels;

/// <summary>
/// Represents an arrow in the game world (shared between client and server)
/// </summary>
public class Arrow
{
    public int ArrowId { get; set; }
    public int HeroId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float DirectionX { get; set; }
    public float DirectionY { get; set; }
    public float Speed { get; set; }
    public float Damage { get; set; }
    public float Accuracy { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsStuck { get; set; } = false;
    public string? StuckTargetType { get; set; } // "Wall", "Enemy", "Hero"
    public int? StuckTargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemoveAt { get; set; } // When to remove the arrow (5 seconds after sticking)
}
