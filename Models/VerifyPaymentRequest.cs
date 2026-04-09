using System.Text.Json.Serialization;

namespace App.Models
{
    public class VerifyPaymentRequest
    {
        [JsonPropertyName("razorpay_order_id")]
        public string? RazorpayOrderId { get; set; }

        // Some clients post camelCase keys instead of Razorpay's snake_case.
        [JsonPropertyName("razorpayOrderId")]
        public string? RazorpayOrderIdCamel { get; set; }

        [JsonPropertyName("razorpay_payment_id")]
        public string? RazorpayPaymentId { get; set; }

        [JsonPropertyName("razorpayPaymentId")]
        public string? RazorpayPaymentIdCamel { get; set; }

        [JsonPropertyName("razorpay_signature")]
        public string? RazorpaySignature { get; set; }

        [JsonPropertyName("razorpaySignature")]
        public string? RazorpaySignatureCamel { get; set; }

        public string? GetOrderId() =>
            !string.IsNullOrWhiteSpace(RazorpayOrderId) ? RazorpayOrderId : RazorpayOrderIdCamel;

        public string? GetPaymentId() =>
            !string.IsNullOrWhiteSpace(RazorpayPaymentId) ? RazorpayPaymentId : RazorpayPaymentIdCamel;

        public string? GetSignature() =>
            !string.IsNullOrWhiteSpace(RazorpaySignature) ? RazorpaySignature : RazorpaySignatureCamel;
    }
}
