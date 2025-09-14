namespace SharedLibrary.Responses;

public class HeroMoveResponse
{
    public int HeroId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public long ServerTimestampMs { get; set; }
}

