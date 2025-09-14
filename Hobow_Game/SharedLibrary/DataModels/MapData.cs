
namespace SharedLibrary.DataModels
{
    public class CollisionObject
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Type { get; set; } = "rectangle";
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class TileData
    {
        public int Gid { get; set; } 
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class TilesetData
    {
        public string Name { get; set; }
        public string Image { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int TileCount { get; set; }
        public int Columns { get; set; }
    }

    public class MapLayer
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<CollisionObject> Objects { get; set; } = new List<CollisionObject>();
        public List<int> Data { get; set; } = new List<int>(); 
        public List<TileData> Tiles { get; set; } = new List<TileData>(); 
    }

    public class MapData
    {
        public string MapId { get; set; }
        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public List<MapLayer> Layers { get; set; } = new List<MapLayer>();
        public List<TilesetData> Tilesets { get; set; } = new List<TilesetData>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

}
