using Hobow_Server.Physics;
using SharedLibrary.DataModels;
using SharedLibrary.Responses;


namespace Hobow_Server.Handlers;

public interface IMapDataHandler
{
    MapDataResponse GetMapData(string mapId);
    MapDataReceivedResponse ConfirmMapDataReceived(string mapId, int heroId);
}

public class MapDataHandler : IMapDataHandler
{
    private readonly ServerPhysicsManager _physics;
    private readonly ILogger<MapDataHandler> _logger;
    private readonly Dictionary<string, MapData> _cachedMapData = new();

    public MapDataHandler(ServerPhysicsManager physics, ILogger<MapDataHandler> logger)
    {
        _physics = physics;
        _logger = logger;
    }

    public MapDataResponse GetMapData(string mapId)
    {
        try
        {

            if (_cachedMapData.TryGetValue(mapId, out var cachedData))
            {
                return new MapDataResponse
                {
                    MapId = mapId,
                    Data = cachedData,
                    Success = true
                };
            }

            var mapData = _physics.GetMapData(mapId);
            
            if (mapData == null)
            {
                _logger.LogWarning($"[MapDataHandler] Map data not found for: {mapId}");
                return new MapDataResponse
                {
                    MapId = mapId,
                    Success = false,
                    Error = $"Map data not found for: {mapId}"
                };
            }

            _cachedMapData[mapId] = mapData;

            
            return new MapDataResponse
            {
                MapId = mapId,
                Data = mapData,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MapDataHandler] Error getting map data for {mapId}: {ex.Message}");
            return new MapDataResponse
            {
                MapId = mapId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    public MapDataReceivedResponse ConfirmMapDataReceived(string mapId, int heroId)
    {
        try
        {
            
            return new MapDataReceivedResponse
            {
                Success = true,
                Message = $"Map data received confirmation for hero {heroId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MapDataHandler] Error confirming map data received: {ex.Message}");
            return new MapDataReceivedResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
