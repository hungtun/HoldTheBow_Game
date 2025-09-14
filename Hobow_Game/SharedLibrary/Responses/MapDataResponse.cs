using SharedLibrary.DataModels;

namespace SharedLibrary.Responses
{
    public class MapDataResponse
    {
        public string MapId { get; set; }
        public MapData Data { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
