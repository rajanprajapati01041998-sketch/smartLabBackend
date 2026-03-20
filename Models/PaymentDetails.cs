using System;
namespace App.Models
{
    public class PaymentDetails
    {
        public decimal amount { get; set; }
        public int paymentModeId { get; set; }
        public int paymentModeTypeId { get; set; }
        public int bankId { get; set; }
        public string? refNo { get; set; }
    }
}