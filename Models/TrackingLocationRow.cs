namespace App.Models
{
    public class TrackingLocationRow
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? AccuracyMeters { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}

