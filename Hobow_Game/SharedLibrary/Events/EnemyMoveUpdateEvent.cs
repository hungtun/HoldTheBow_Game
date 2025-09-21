namespace SharedLibrary.Events;

/// <summary>
/// Event sent by server to all clients when enemy position is updated
/// </summary>
public class EnemyMoveUpdateEvent
{
    public int EnemyId { get; set; }
    public string EnemyName { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public long ServerTimestampMs { get; set; }
    public bool IsMoving { get; set; } // Whether enemy is currently moving
    public string MovementType { get; set; } = "AI"; // "AI", "Chase", "Attack", etc.
}
