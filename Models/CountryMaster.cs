using System;

namespace LISD.Models
{
    public class CountryMaster
    {
        public int CountryId { get; set; }

        public string? CountryName { get; set; }

        public string? Currency { get; set; }

        public decimal ConversionFactor { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedOn { get; set; }

        public int CreatedBy { get; set; }

        public DateTime? LastModifiedOn { get; set; }

        public int? LastModifiedBy { get; set; }

        public string? IPAddress { get; set; }
    }
}
