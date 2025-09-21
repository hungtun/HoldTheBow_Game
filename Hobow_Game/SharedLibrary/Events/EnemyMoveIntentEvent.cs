namespace SharedLibrary.Events;

/// <summary>
/// Event sent by server AI when enemy intends to move
/// </summary>
public class EnemyMoveIntentEvent
{
    public int EnemyId { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float MoveSpeed { get; set; }
    public long ServerTimestampMs { get; set; } // Server timestamp for synchronization
}
