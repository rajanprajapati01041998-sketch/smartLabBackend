namespace LISDBACKEND.Models
{
    public class FieldBoyLocationDto
    {
        public int FieldBoyId { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double AccuracyMeters { get; set; }

        public string? CapturedAt { get; set; }
    }
}