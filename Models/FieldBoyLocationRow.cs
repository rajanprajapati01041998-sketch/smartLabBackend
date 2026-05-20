namespace App.Models
{
    public class FieldBoyLocationRow
    {
        public long Id { get; set; }
        public int FieldBoyId { get; set; }
        public string? FieldBoyName { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? AccuracyMeters { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}

