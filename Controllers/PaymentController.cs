using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;
using App.Data;
using App.Models;
using App.Settings;

namespace App.Controllers
{
    [ApiController]
    [Route("api/payment")]
    public class PaymentController : ControllerBase
    {
        private readonly RazorpaySettings _settings;
        private readonly AppDbContext _context;

        public PaymentController(IOptions<RazorpaySettings> settings, AppDbContext context)
        {
            _settings = settings.Value;
            _context = context;
        }

        [HttpPost("create-order")]
        public IActionResult CreateOrder(CreateOrderRequest request)
        {
            if (request == null || request.Amount <= 0)
                return BadRequest("Invalid request");

            // 🔍 DEBUG
            Console.WriteLine("KEY: " + _settings.Key);
            Console.WriteLine("SECRET: " + _settings.Secret);

            try
            {
                // 🔥 HARDCODE TEST (replace once working)
                var client = new RazorpayClient(
                    _settings.Key,
                    _settings.Secret
                );

                var options = new Dictionary<string, object>
                {
                    { "amount", (int)(request.Amount * 100) },
                    { "currency", "INR" },
                    { "receipt", string.IsNullOrEmpty(request.ReceiptNo)
                                    ? Guid.NewGuid().ToString()
                                    : request.ReceiptNo }
                };

                var order = client.Order.Create(options);

                return Ok(new
                {
                    orderId = order["id"].ToString(),
                    amount = order["amount"],
                    currency = order["currency"],
                    clientId = request.ClientId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = "Razorpay error",
                    message = ex.Message,
                    keyUsed = _settings.Key
                });
            }
        }

        [HttpPost("verify")]
        public IActionResult VerifyPayment(
            [FromForm] string razorpay_order_id,
            [FromForm] string razorpay_payment_id,
            [FromForm] string razorpay_signature,
            [FromForm] decimal amount)
        {
            string payload = razorpay_order_id + "|" + razorpay_payment_id;
            var generatedSignature = ComputeHmacSha256(payload, _settings.Secret);

            if (generatedSignature != razorpay_signature)
                return BadRequest("Invalid signature");

            var data = new LabAdvanceAmount
            {
                HospId = 1,
                BranchId = 1,
                LabReceiptID = 1,
                LabReceiptNo = razorpay_order_id,
                ClientID = 1,
                DepositDate = DateTime.Now,
                PaymentModeId = 1,
                PaymentMode = "Online",
                Amount = amount,
                TransactionId = razorpay_payment_id,
                Status = "Verify",
                StatusId = 1,
                IsCancel = false,
                CreatedBy = 1,
                CreatedOn = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UniqueId = Guid.NewGuid().ToString()
            };

            _context.LabAdvanceAmounts.Add(data);
            _context.SaveChanges();

            return Ok("Payment verified & saved");
        }

        private string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}