using System.ComponentModel.DataAnnotations;

namespace App.DTOs
{
    public class HelpDeskRequestDto
    {
        [Required]
        public string branchId { get; set; }

        [Required]
        public string typeId { get; set; }

        public string? uhid { get; set; }
        public string? ipdNo { get; set; }
        public string? labNo { get; set; }

        public string? fromDate { get; set; }
        public string? toDate { get; set; }

        public string? barCode { get; set; }

        public string? subCategoryId { get; set; }
        public string? subSubCategoryId { get; set; }

        public string? investigationName { get; set; }
        public string? patientName { get; set; }

        [Required]
        public string branchIdList { get; set; }

        public string? corporateId { get; set; }

        // Default value
        public string roleId { get; set; } = "0";

        public string? filter { get; set; }

        internal object GetProperty(string v)
        {
            throw new NotImplementedException();
        }

        internal bool TryGetProperty(string key, out object value)
        {
            throw new NotImplementedException();
        }
    }
}