namespace SharedLibrary.Events;

/// <summary>
/// Event sent from server to all clients when an arrow is spawned
/// </summary>
public class ArrowSpawnedEvent
{
    public int ArrowId { get; set; }
    public int HeroId { get; set; }
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float DirectionX { get; set; }
    public float DirectionY { get; set; }
    public float Speed { get; set; }
    public float Damage { get; set; }
    public float Accuracy { get; set; } // 0.0 to 1.0
    public long ServerTimestampMs { get; set; }
}
