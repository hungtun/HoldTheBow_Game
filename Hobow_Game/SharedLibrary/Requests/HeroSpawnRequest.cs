namespace SharedLibrary.Requests;

public class HeroSpawnRequest
{
    public int HeroId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string MapId { get; set; } = "Home";
    public float HeroRadius { get; set; } = 0.25f;
    public float ProbeOffsetY { get; set; } = -0.3f;
    public float HitboxCenterOffsetX { get; set; }
    public float HitboxCenterOffsetY { get; set; }
    public float HitboxHalfSizeX { get; set; }
    public float HitboxHalfSizeY { get; set; }
}
