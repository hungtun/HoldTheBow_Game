namespace SharedLibrary.Responses;

public class SessionStatusResponse
{
    public bool IsOnline { get; set; }
    public string Message { get; set; } = string.Empty;
}
