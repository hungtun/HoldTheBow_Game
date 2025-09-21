namespace SharedLibrary.Events;

/// <summary>
/// Event sent by client to server when hero intends to move
/// </summary>
public class HeroMoveIntentEvent
{
    public int HeroId { get; set; }
    public string Direction { get; set; } = "down"; // "left", "right", "up", "down"
    public float Speed { get; set; } // Speed of the hero's movement
    public long ClientTimestampMs { get; set; } // Client timestamp for latency compensation
}
