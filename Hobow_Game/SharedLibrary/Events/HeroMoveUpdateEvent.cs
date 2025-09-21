namespace SharedLibrary.Events;

/// <summary>
/// Event sent by server to all clients when hero position is updated
/// </summary>
public class HeroMoveUpdateEvent
{
    public int HeroId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public long ServerTimestampMs { get; set; }
    public string Direction { get; set; } = ""; // Direction of movement for animation
    public bool IsMoving { get; set; } // Whether hero is currently moving
}
