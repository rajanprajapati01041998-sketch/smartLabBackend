using System;

namespace LISD.Models
{
    public class DoctorDepartmentMaster
    {
        public int DepartmentId { get; set; }

        public string? Department { get; set; }

        public int HospId { get; set; }

        public bool IsActive { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public int? LastModifiedBy { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public string? IpAddress { get; set; }
    }
}
