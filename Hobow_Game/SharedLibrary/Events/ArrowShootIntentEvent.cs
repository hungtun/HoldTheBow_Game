namespace SharedLibrary.Events;

/// <summary>
/// Event sent from client to server when player intends to shoot an arrow
/// </summary>
public class ArrowShootIntentEvent
{
    public int HeroId { get; set; }
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float DirectionX { get; set; }
    public float DirectionY { get; set; }
    public float ChargePercent { get; set; } // 0.0 to 1.0
    public float ChargeTime { get; set; } // in seconds
    public long ClientTimestampMs { get; set; }
}
