namespace SharedLibrary.Events;

/// <summary>
/// Event sent by client when bow state changes (charging, angle, etc.)
/// </summary>
public class BowStateIntentEvent
{
    public int HeroId { get; set; }
    public float AngleDeg { get; set; } // Bow angle in degrees
    public bool IsCharging { get; set; } // Whether the bow is currently charging
    public float ChargePercent { get; set; } // Charge percentage (0.0 to 1.0)
    public long ClientTimestampMs { get; set; } // Client timestamp for synchronization
}
