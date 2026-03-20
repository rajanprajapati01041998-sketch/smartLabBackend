using System;

namespace LISD.Models
{
    public class DepartmentRequest
    {
        public int DepartmentId { get; set; }

        public string? Department { get; set; }

        public int HospId { get; set; }

        public int UserId { get; set; }

        public string? IpAddress { get; set; }
    }
}
