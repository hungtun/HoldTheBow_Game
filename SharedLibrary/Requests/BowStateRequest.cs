namespace SharedLibrary.Requests
{
    public class BowStateRequest
    {
        public int HeroId { get; set; }
        public float AngleDeg { get; set; } 
        public bool IsCharging { get; set; }
        public float ChargePercent { get; set; }
    }
}


