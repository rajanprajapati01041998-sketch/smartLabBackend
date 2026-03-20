using System;

namespace LISD.Models
{
    public class LocationMaster
    {
        public int LocationId { get; set; }

        public string? LocationName { get; set; }

        public bool IsParent { get; set; }

        public int ParentId { get; set; }

        public bool IsActive { get; set; }

        public string? Pincode { get; set; }

        public string? IPAddress { get; set; }

        public DateTime CreatedOn { get; set; }

        public int CreatedBy { get; set; }

        public DateTime? LastModifiedOn { get; set; }

        public int? LastModifiedBy { get; set; }

        public bool IsState { get; set; }
    }
}
