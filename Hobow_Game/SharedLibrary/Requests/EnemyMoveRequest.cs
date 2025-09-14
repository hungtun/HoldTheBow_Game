namespace SharedLibrary.Requests
{
    public class EnemyMoveRequest
    {
        public int EnemyId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float MoveSpeed { get; set; }
    }
}