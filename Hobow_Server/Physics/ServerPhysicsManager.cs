using Microsoft.Xna.Framework;
using SharedLibrary.DataModels;

namespace Hobow_Server.Physics
{
    public class ServerPhysicsManager
    {
        private readonly ILogger<ServerPhysicsManager> _logger;
        private readonly Dictionary<int, HeroPhysicsBody> _heroBodies = new();
        private readonly Dictionary<string, List<StaticCollision>> _mapCollisions = new();
        
        public ServerPhysicsManager(ILogger<ServerPhysicsManager> logger)
        {
            _logger = logger;
            _logger.LogInformation("[ServerPhysicsManager] Physics world initialized");
        }
        
        public void Update(float deltaTime)
        {
            // Validate deltaTime
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0)
            {
                deltaTime = 1f / 60f; 
            }
            
        }
        
        public bool CreateHeroBody(int heroId, Vector2 position, float radius = 0.25f)
        {
            if (_heroBodies.ContainsKey(heroId))
            {
                return false;
            }
            
            _heroBodies[heroId] = new HeroPhysicsBody
            {
                Position = position,
                Radius = radius,
                Velocity = Vector2.Zero
            };
            
            _logger.LogInformation($"[ServerPhysicsManager] Created hero body {heroId} at {position}");
            return true;
        }
        
        public void RemoveHeroBody(int heroId)
        {
            if (_heroBodies.Remove(heroId))
            {
                _logger.LogInformation($"[ServerPhysicsManager] Removed hero body {heroId}");
            }
        }
        
        public bool HasHeroBody(int heroId)
        {
            return _heroBodies.ContainsKey(heroId);
        }
        
        public bool MoveHero(int heroId, Vector2 targetPosition, float speed)
        {
            if (!_heroBodies.TryGetValue(heroId, out var body))
            {
                return false;
            }
            
            Vector2 delta = targetPosition - body.Position;
            if (delta.LengthSquared() > 0.001f)
            {
                Vector2 direction = Vector2.Normalize(delta);
                body.Velocity = direction * speed;
            }
            else
            {
                body.Velocity = Vector2.Zero;
            }
            
            return true;
        }
        
        public Vector2 GetHeroPosition(int heroId)
        {
            if (_heroBodies.TryGetValue(heroId, out var body))
            {
                if (float.IsNaN(body.Position.X) || float.IsInfinity(body.Position.X) ||
                    float.IsNaN(body.Position.Y) || float.IsInfinity(body.Position.Y))
                {
                    body.Position = Vector2.Zero;
                }
                return body.Position;
            }
            return Vector2.Zero;
        }
        
        public void SetHeroPosition(int heroId, Vector2 position)
        {
            if (_heroBodies.TryGetValue(heroId, out var body))
            {
                if (float.IsNaN(position.X) || float.IsInfinity(position.X) ||
                    float.IsNaN(position.Y) || float.IsInfinity(position.Y))
                {
                    _logger.LogWarning($"[ServerPhysicsManager] Invalid position provided for hero {heroId}, ignoring");
                    return;
                }
                
                body.Position = position;
                body.Velocity = Vector2.Zero; 
            }
        }
        
        public bool IsPositionValid(Vector2 position, float radius = 0.25f)
        {
            foreach (var mapCollisions in _mapCollisions.Values)
            {
                foreach (var collision in mapCollisions)
                {
                    if (collision.Type == CollisionType.Rectangle)
                    {
                        if (IsCircleRectCollision(position, radius, collision.Position, collision.Width, collision.Height))
                        {
                            return false;
                        }
                    }
                    else if (collision.Type == CollisionType.Circle)
                    {
                        float distance = Vector2.Distance(position, collision.Position);
                        if (distance < radius + collision.Radius)
                        {
                            return false;
                        }
                    }
                }
            }
            
            return true;
        }
        
        public void LoadMapCollisions(string mapId, List<MapCollisionData> collisions)
        {
            if (_mapCollisions.ContainsKey(mapId))
            {
                _mapCollisions[mapId].Clear();
            }
            else
            {
                _mapCollisions[mapId] = new List<StaticCollision>();
            }
            
            foreach (var collision in collisions)
            {
                _mapCollisions[mapId].Add(new StaticCollision
                {
                    Position = collision.Position,
                    Type = collision.Type,
                    Width = collision.Width,
                    Height = collision.Height,
                    Radius = collision.Radius
                });
            }
            
            _logger.LogInformation($"[ServerPhysicsManager] Loaded {collisions.Count} collision objects for map {mapId}");
        }
        
        private void CheckMapCollision(HeroPhysicsBody heroBody)
        {
            foreach (var mapCollisions in _mapCollisions.Values)
            {
                foreach (var collision in mapCollisions)
                {
                    bool hasCollision = false;
                    
                    if (collision.Type == CollisionType.Rectangle)
                    {
                        hasCollision = IsCircleRectCollision(heroBody.Position, heroBody.Radius, 
                            collision.Position, collision.Width, collision.Height);
                    }
                    else if (collision.Type == CollisionType.Circle)
                    {
                        float distance = Vector2.Distance(heroBody.Position, collision.Position);
                        hasCollision = distance < heroBody.Radius + collision.Radius;
                    }
                    
                    if (hasCollision)
                    {
                        heroBody.Velocity = Vector2.Zero;
                        return;
                    }
                }
            }
        }
        
        private bool IsCircleRectCollision(Vector2 circlePos, float circleRadius, Vector2 rectPos, float rectWidth, float rectHeight)
        {
            float closestX = Math.Max(rectPos.X - rectWidth/2, Math.Min(circlePos.X, rectPos.X + rectWidth/2));
            float closestY = Math.Max(rectPos.Y - rectHeight/2, Math.Min(circlePos.Y, rectPos.Y + rectHeight/2));
            
            float distanceX = circlePos.X - closestX;
            float distanceY = circlePos.Y - closestY;
            float distanceSquared = distanceX * distanceX + distanceY * distanceY;
            
            return distanceSquared < circleRadius * circleRadius;
        }
        
        public void Dispose()
        {
            _heroBodies.Clear();
            _mapCollisions.Clear();
            _logger.LogInformation("[ServerPhysicsManager] Physics world disposed");
        }

        /// <summary>
        /// Lấy map data để gửi cho client
        /// </summary>
        public MapData GetMapData(string mapId)
        {
            try
            {
                if (!_mapCollisions.ContainsKey(mapId))
                {
                    _logger.LogWarning($"[ServerPhysicsManager] Map {mapId} not found in physics manager");
                    return null;
                }

                var mapData = new MapData
                {
                    MapId = mapId,
                    Name = mapId,
                    Width = 25, // Default map size
                    Height = 25,
                    TileWidth = 16,
                    TileHeight = 16
                };

                // Tạo collision layer
                var collisionLayer = new MapLayer
                {
                    Name = "Collisions",
                    Type = "objectgroup",
                    Width = mapData.Width,
                    Height = mapData.Height
                };

                // Convert collision objects
                foreach (var collision in _mapCollisions[mapId])
                {
                    var collisionObject = new CollisionObject
                    {
                        X = collision.Position.X,
                        Y = collision.Position.Y,
                        Width = collision.Width,
                        Height = collision.Height,
                        Type = collision.Type == CollisionType.Rectangle ? "rectangle" : "circle"
                    };

                    collisionLayer.Objects.Add(collisionObject);
                }

                mapData.Layers.Add(collisionLayer);

                _logger.LogInformation($"[ServerPhysicsManager] Exported map data for {mapId}: {collisionLayer.Objects.Count} collision objects");
                return mapData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ServerPhysicsManager] Error exporting map data for {mapId}: {ex.Message}");
                return null;
            }
        }
    }
    
    public class HeroPhysicsBody
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; }
    }
    
    public class StaticCollision
    {
        public Vector2 Position { get; set; }
        public CollisionType Type { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Radius { get; set; }
    }
    
    public class MapCollisionData
    {
        public Vector2 Position { get; set; }
        public CollisionType Type { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Radius { get; set; }
    }
    
    public enum CollisionType
    {
        Rectangle,
        Circle
    }
}
