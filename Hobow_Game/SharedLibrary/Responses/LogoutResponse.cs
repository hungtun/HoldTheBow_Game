namespace SharedLibrary.Responses;

public class LogoutResponse
{
    public int HeroId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public long ServerTimestampMs { get; set; }
}
