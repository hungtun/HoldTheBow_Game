namespace Hobow_Server.Models;
public class EnemySpawnPoint
{
    public int Id { get; set; }
    public string MapId { get; set; } = "Home";
    public int EnemyDefinitionId { get; set; }
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float SpawnRadius { get; set; } = 30f;
    public int SpawnIntervalSec { get; set; } = 10;
    public int MaxAlive { get; set; } = 3;
    public bool IsEnabled { get; set; } = true;
}