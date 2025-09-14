namespace SharedLibrary.Responses;

public class HeroSpawnResponse
{
    public int HeroId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public long ServerTimestampMs { get; set; }
}
