namespace SharedLibrary.Requests
{
    public class MapDataRequest
    {
        public string MapId { get; set; }
    }

    public class MapDataReceivedRequest
    {
        public string MapId { get; set; }
        public int HeroId { get; set; }
    }
}
