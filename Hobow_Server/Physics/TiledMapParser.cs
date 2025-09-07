using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Hobow_Server.Physics
{
    public class TiledMapParser
    {
        private readonly ILogger<TiledMapParser> _logger;
        
        public TiledMapParser(ILogger<TiledMapParser> logger)
        {
            _logger = logger;
        }
        
        public List<MapCollisionData> ParseMapFile(string filePath)
        {
            _logger.LogInformation($"[TiledMapParser] Starting to parse file: {filePath}");
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError($"[TiledMapParser] Map file not found: {filePath}");
                    return new List<MapCollisionData>();
                }
                
                string jsonContent = File.ReadAllText(filePath);
                _logger.LogInformation($"[TiledMapParser] JSON content length: {jsonContent.Length}");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var mapData = JsonSerializer.Deserialize<TiledMapData>(jsonContent, options);
                
                if (mapData == null)
                {
                    _logger.LogError("[TiledMapParser] Failed to deserialize map data");
                    return new List<MapCollisionData>();
                }
                
                _logger.LogInformation($"[TiledMapParser] Map data: {mapData.Width}x{mapData.Height}, TileSize: {mapData.TileWidth}x{mapData.TileHeight}");
                
                var collisions = new List<MapCollisionData>();
                
                // Parse layers
                _logger.LogInformation($"[TiledMapParser] Found {mapData.Layers.Count} layers in map");
                foreach (var layer in mapData.Layers)
                {
                    _logger.LogInformation($"[TiledMapParser] Layer: '{layer.Name}'");
                    if (layer.Name.ToLower().Contains("collision") || layer.Name.ToLower().Contains("obstacle"))
                    {
                        _logger.LogInformation($"[TiledMapParser] Processing collision layer: '{layer.Name}'");
                        var layerCollisions = ParseLayer(layer, mapData);
                        _logger.LogInformation($"[TiledMapParser] Found {layerCollisions.Count} collision objects in layer '{layer.Name}'");
                        collisions.AddRange(layerCollisions);
                    }
                }
                
                _logger.LogInformation($"[TiledMapParser] Parsed {collisions.Count} collision objects from {filePath}");
                return collisions;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[TiledMapParser] Error parsing map file {filePath}: {ex.Message}");
                return new List<MapCollisionData>();
            }
        }
        
        private List<MapCollisionData> ParseLayer(TiledLayer layer, TiledMapData mapData)
        {
            var collisions = new List<MapCollisionData>();
            
            // --- CẬP NHẬT THEO CÔNG THỨC YÊU CẦU ---
            // Đảm bảo server dùng đúng scale và offset như client
            float scale = 1f / 16f;
            float mapPixelWidth = mapData.Width * mapData.TileWidth;
            float mapPixelHeight = mapData.Height * mapData.TileHeight;
            float offsetX = -(mapPixelWidth * scale) / 2f;
            float offsetY = -(mapPixelHeight * scale) / 2f;
            // ----------------------------------------
            
            if (layer.Objects != null)
            {
                foreach (var obj in layer.Objects)
                {
                    // Công thức Tiled -> Unity
                    float unityX = (obj.X + obj.Width / 2f) * scale + offsetX;
                    float unityY = (mapPixelHeight - (obj.Y + obj.Height / 2f)) * scale + offsetY;
                    
                    if (obj.Width > 0 && obj.Height > 0)
                    {
                        collisions.Add(new MapCollisionData
                        {
                            Position = new Vector2(unityX, unityY),
                            Type = CollisionType.Rectangle,
                            Width = obj.Width * scale,
                            Height = obj.Height * scale
                        });
                        
                        _logger.LogInformation($"[TiledMapParser] Rect Tiled({obj.X:F2},{obj.Y:F2},{obj.Width:F2},{obj.Height:F2}) -> Unity({unityX:F2},{unityY:F2}) size({obj.Width * scale:F2},{obj.Height * scale:F2})");
                    }
                    else if (obj.Radius > 0)
                    {
                        // Công thức cho hình tròn (Tiled lưu X,Y là tâm)
                        unityX = obj.X * scale + offsetX;
                        unityY = (mapPixelHeight - obj.Y) * scale + offsetY;
                        
                        collisions.Add(new MapCollisionData
                        {
                            Position = new Vector2(unityX, unityY),
                            Type = CollisionType.Circle,
                            Radius = obj.Radius * scale
                        });
                        
                        _logger.LogInformation($"[TiledMapParser] Circle Tiled({obj.X:F2},{obj.Y:F2}) -> Unity({unityX:F2},{unityY:F2}) radius({obj.Radius * scale:F2})");
                    }
                }
            }
            
            if (layer.Data != null)
            {
                _logger.LogWarning("[TiledMapParser] Tile Layer parsing is not implemented for collision data.");
            }
            
            return collisions;
        }
    }
}

public class TiledMapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public List<TiledLayer> Layers { get; set; } = new();
    }
public class TiledObject
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Radius { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }
    public class TiledLayer
    {
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int[]? Data { get; set; }
        public List<TiledObject> Objects { get; set; }
    }