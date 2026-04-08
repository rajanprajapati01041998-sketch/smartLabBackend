namespace App.Models
{
    public class CreateOrderRequest
    {
        public decimal Amount { get; set; }
        public int ClientId { get; set; }
        public string ReceiptNo { get; set; }
    }
}