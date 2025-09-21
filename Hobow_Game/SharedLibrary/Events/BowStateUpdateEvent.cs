namespace SharedLibrary.Events;

/// <summary>
/// Event sent by server to all clients when bow state is updated
/// </summary>
public class BowStateUpdateEvent
{
    public int HeroId { get; set; }
    public float AngleDeg { get; set; } // Bow angle in degrees
    public bool IsCharging { get; set; } // Whether the bow is currently charging
    public float ChargePercent { get; set; } // Charge percentage (0.0 to 1.0)
    public long ServerTimestampMs { get; set; } // Server timestamp for synchronization
    public string Action { get; set; } = "Update"; // "Update", "Shoot", "Stop"
}