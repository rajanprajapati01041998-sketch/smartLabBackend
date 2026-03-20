namespace App.Models
{
    public class OutSourceLabModel
    {
        public int OutSourceLabId { get; set; }

        public int HospId { get; set; }          // ✅ Added
        public int BranchId { get; set; }

        public string OutSourceLab { get; set; }

        public string BranchName { get; set; }   // (Used only in GET)

        public string ContactPerson { get; set; }
        public string ContactNumber { get; set; }
        public string Address { get; set; }

        public bool IsActive { get; set; }

        public int CreatedBy { get; set; }       // ✅ Added
        public string IpAddress { get; set; }    // ✅ Added
    }
}