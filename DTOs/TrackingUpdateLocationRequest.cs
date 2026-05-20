using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace App.DTOs
{
    public class TrackingUpdateLocationRequest
    {
        // Prefer FieldBoyId. UserId is kept for backward compatibility with older clients.
        [JsonPropertyName("fieldBoyId")]
        public int? FieldBoyId { get; set; }

        [JsonPropertyName("userId")]
        public int? UserId { get; set; }

        [Required]
        public decimal Latitude { get; set; }

        [Required]
        public decimal Longitude { get; set; }

        public decimal? AccuracyMeters { get; set; }

        // Optional client timestamp (ISO string). If not provided, server UTC is used.
        public DateTime? CapturedAtUtc { get; set; }
    }
}
