namespace SharedLibrary.Events;

public class HeroDamagedEvent
{
    public int HeroId { get; set; }
    public int Damage { get; set; }
    public int NewHealth { get; set; }
    public string Source { get; set; } = "Enemy";
    public long ServerTimestampMs { get; set; }
}


