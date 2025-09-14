namespace Hobow_Server.Models;
public class EnemyDefinition
{
    public int Id { get; set; }
    public string EnemyName { get; set; } = "Slime";
    public int MaxHealth { get; set; } = 100;
    public int Attack { get; set; } = 10;
    public float MoveSpeed { get; set; } = 2f;
    public string PrefabKey { get; set; } = "Slime";
    public string? Notes { get; set; }
}