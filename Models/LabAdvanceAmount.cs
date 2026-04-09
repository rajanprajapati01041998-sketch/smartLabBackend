using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App.Models
{
    [Table("LabAdvanceAmount")]
    public class LabAdvanceAmount
    {
        [Key]
        public int LabReceiptID { get; set; }

        public int HospId { get; set; }
        public int BranchId { get; set; }
        public string? LabReceiptNo { get; set; }
        public int ClientID { get; set; }
        public DateTime? DepositDate { get; set; }
        public int PaymentModeId { get; set; }
        public string? PaymentMode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string? ChequeCardNo { get; set; }
        public DateTime? ChequeCardDate { get; set; }
        public int? PaymentBankId { get; set; }
        public string? PayMode { get; set; }
        public string? TransactionId { get; set; }
        public string? Status { get; set; }
        public int? StatusId { get; set; }
        public bool IsCancel { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public int? LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? IpAddress { get; set; }
        public string? UniqueId { get; set; }
        public string? CancelReason { get; set; }
        public string? remarks { get; set; }
        public int? CancelBy { get; set; }
        public int? VerifyBy { get; set; }
        public DateTime? CancelOn { get; set; }
        public DateTime? VerifyOn { get; set; }
    }
}