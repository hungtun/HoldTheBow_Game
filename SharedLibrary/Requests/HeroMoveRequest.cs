namespace SharedLibrary.Requests;

public class HeroMoveRequest
{
    public int HeroId { get; set; }
    public string Direction { get; set; } = "down"; // "left", "right", "up", "down"
    public float Speed { get; set; } // Speed of the hero's movement
}

