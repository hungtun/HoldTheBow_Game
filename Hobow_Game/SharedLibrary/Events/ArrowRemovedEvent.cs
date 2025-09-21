namespace SharedLibrary.Events;

/// <summary>
/// Event sent from server to all clients when an arrow is removed (after 5 seconds)
/// </summary>
public class ArrowRemovedEvent
{
    public int ArrowId { get; set; }
    public long ServerTimestampMs { get; set; }
}
