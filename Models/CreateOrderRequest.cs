
using System;
namespace App.Models
{
    public class CreateOrderRequest
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int ClientId { get; set; }
        public int PaymentModeId { get; set; }
        public int CreatedBy { get; set; }
        public decimal Amount { get; set; }

        public string? Currency { get; set; }
        public string? Receipt { get; set; }
        public string? PaymentMode { get; set; }
        public string? Remarks { get; set; }
    }
}