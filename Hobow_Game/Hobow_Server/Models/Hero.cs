namespace Hobow_Server.Models;

public class Hero
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int MaxHealth { get; set; } = 100;
    public int Damage { get; set; } = 50;
    
    public User User { get; set; } = null!;
}