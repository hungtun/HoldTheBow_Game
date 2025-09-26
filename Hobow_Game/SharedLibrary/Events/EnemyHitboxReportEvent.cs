namespace SharedLibrary.Events;

public class EnemyHitboxReportEvent
{
    public int EnemyId { get; set; }
    public float CenterOffsetX { get; set; }
    public float CenterOffsetY { get; set; }
    public float HalfSizeX { get; set; }
    public float HalfSizeY { get; set; }
}


