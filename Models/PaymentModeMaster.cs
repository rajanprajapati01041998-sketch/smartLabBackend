using System;

namespace LISD.Models
{
    public class PaymentModeMaster
    {
        public int PaymentModeId { get; set; }

        public int HospId { get; set; }

        public string? PaymentModeName { get; set; }

        public string? PayModeType { get; set; }

        public int PayModeTypeId { get; set; }

        public bool IsRefundAllowed { get; set; }

        public bool IsActive { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedOn { get; set; }

        public int? LastModifiedBy { get; set; }

        public DateTime? LastModifiedOn { get; set; }

        public string? IpAddress { get; set; }

        public string? UniqueId { get; set; }

        public decimal Amount { get; set; }
    }
}
