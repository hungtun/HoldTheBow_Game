namespace SharedLibrary.Responses
{
    public class BowStateResponse
    {
        public int HeroId { get; set; }
        public float AngleDeg { get; set; }
        public bool IsCharging { get; set; }
        public float ChargePercent { get; set; }
        public long ServerTimestampMs { get; set; }
    }
}


