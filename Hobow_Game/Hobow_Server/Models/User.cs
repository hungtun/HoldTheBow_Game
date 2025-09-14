namespace Hobow_Server.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Player";
    public string? ActiveSessionId { get; set; } 
    public List<Hero> Heroes { get; set; } = new();
}