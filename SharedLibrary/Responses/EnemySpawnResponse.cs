namespace SharedLibrary.Responses
{
    public class EnemySpawnResponse
    {
        public int EnemyId { get; set; }
        public string EnemyName { get; set; } = "Slime";
        public string MapId { get; set; } = "Home";
        public float X { get; set; }
        public float Y { get; set; }
        public int Health { get; set; }
        public int Attack { get; set; }
        public float MoveSpeed { get; set; }
        public long ServerTimestampMs { get; set; }
    }
}