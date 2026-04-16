using App.Data;
using App.Models;
using App.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RazorpaySettings _razorpaySettings;

        public PaymentController(AppDbContext context, IOptions<RazorpaySettings> razorpaySettings)
        {
            _context = context;
            _razorpaySettings = razorpaySettings.Value;
        }

        // ===========================
        // CREATE ORDER (NO DB SAVE)
        // ===========================
        [HttpPost("create-order")]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request == null || request.Amount <= 0)
                return BadRequest("Invalid request");

            var client = new RazorpayClient(_razorpaySettings.Key, _razorpaySettings.Secret);

            int amount = Convert.ToInt32(request.Amount * 100);

            var options = new Dictionary<string, object>
            {
                { "amount", amount },
                { "currency", "INR" },
                { "receipt", Guid.NewGuid().ToString() }
            };

            var order = client.Order.Create(options);

            return Ok(new
            {
                key = _razorpaySettings.Key,
                orderId = order["id"].ToString(),
                amount = amount
            });
        }

        // ===========================
        // VERIFY + SAVE DB
        // ===========================
        [HttpPost("verify-payment")]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Invalid request",
                        data = (object?)null
                    });
                }

                var orderId = request.GetOrderId();
                var paymentId = request.GetPaymentId();
                var signature = request.GetSignature();

                if (string.IsNullOrWhiteSpace(orderId) ||
                    string.IsNullOrWhiteSpace(paymentId) ||
                    string.IsNullOrWhiteSpace(signature))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Invalid payment data",
                        data = (object?)null
                    });
                }

                if (string.IsNullOrWhiteSpace(_razorpaySettings.Key) ||
                    string.IsNullOrWhiteSpace(_razorpaySettings.Secret))
                {
                    return StatusCode(500, new
                    {
                        status = false,
                        message = "Razorpay settings are missing",
                        data = (object?)null
                    });
                }

                var payload = $"{orderId}|{paymentId}";
                var expectedSignature = ComputeHmacSha256Hex(_razorpaySettings.Secret, payload);

                if (!FixedTimeEqualsHex(expectedSignature, signature))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Invalid payment signature",
                        data = (object?)null
                    });
                }

                var client = new RazorpayClient(_razorpaySettings.Key, _razorpaySettings.Secret);
                var payment = client.Payment.Fetch(paymentId);

                if (payment == null)
                {
                    return NotFound(new
                    {
                        status = false,
                        message = "Payment not found on Razorpay",
                        data = (object?)null
                    });
                }

                string razorpayOrderId = payment["order_id"]?.ToString()?.Trim() ?? "";
                string razorpayPaymentId = payment["id"]?.ToString()?.Trim() ?? "";
                string paymentStatus = payment["status"]?.ToString()?.Trim()?.ToLower() ?? "";

                decimal razorpayAmount = 0;
                if (payment["amount"] != null)
                {
                    razorpayAmount = Convert.ToDecimal(payment["amount"]) / 100m;
                }

                if (string.IsNullOrWhiteSpace(razorpayOrderId))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Razorpay order id not found",
                        data = (object?)null
                    });
                }

                if (!string.Equals(razorpayOrderId, orderId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Order id mismatch",
                        data = (object?)null
                    });
                }

                if (paymentStatus != "captured" && paymentStatus != "authorized")
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = $"Payment not successful. Status: {paymentStatus}",
                        data = (object?)null
                    });
                }

                int hospId = request.HospId;
                int branchId = request.BranchId;
                int clientId = request.ClientId;
                int createdBy = request.CreatedBy;
                int paymentModeId = request.PaymentModeId;

                if (hospId <= 0 || branchId <= 0 || clientId <= 0 || createdBy <= 0 || paymentModeId <= 0)
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "HospId, BranchId, ClientId, CreatedBy and PaymentModeId must be greater than 0",
                        data = (object?)null
                    });
                }

                string orderIdValue = razorpayOrderId;
                string paymentIdValue = razorpayPaymentId;
                decimal paidAmount = request.Amount > 0 ? request.Amount : razorpayAmount;

                var existing = await _context.LabAdvanceAmounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TransactionId == paymentIdValue);

                if (existing != null)
                {
                    return Ok(new
                    {
                        status = true,
                        message = "Payment already saved",
                        data = new
                        {
                            orderId = orderIdValue,
                            paymentId = paymentIdValue,
                            receiptNo = existing.LabReceiptNo
                        }
                    });
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Length > 20)
                    ipAddress = ipAddress.Substring(0, 20);

                var spParams = new[]
                {
            new SqlParameter("@hospId", SqlDbType.Int) { Value = hospId },
            new SqlParameter("@branchId", SqlDbType.Int) { Value = branchId },
            new SqlParameter("@userId", SqlDbType.Int) { Value = createdBy },
            new SqlParameter("@ClientID", SqlDbType.Int) { Value = clientId },
            new SqlParameter("@DepositDate", SqlDbType.Date) { Value = DateTime.UtcNow.Date },
            new SqlParameter("@PaymentMode", SqlDbType.NVarChar, 50)
            {
                Value = "ONLINE"
            },
            new SqlParameter("@PaymentModeId", SqlDbType.Int) { Value = paymentModeId },
            new SqlParameter("@PaidAmount", SqlDbType.Decimal)
            {
                Precision = 16,
                Scale = 6,
                Value = paidAmount
            },
            new SqlParameter("@ChequeCardNo", SqlDbType.NVarChar, 50)
            {
                Value = orderIdValue
            },
            new SqlParameter("@ChequeCardDate", SqlDbType.Date)
            {
                Value = DBNull.Value
            },
            new SqlParameter("@PaymentBankId", SqlDbType.Int)
            {
                Value = 0
            },
            new SqlParameter("@PayMode", SqlDbType.NVarChar, 20)
            {
                Value = "Razorpay"
            },
            new SqlParameter("@TransactionId", SqlDbType.NVarChar, 50)
            {
                Value = paymentIdValue
            },
            new SqlParameter("@remarks", SqlDbType.NVarChar, 512)
            {
                Value = string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()
            },
            new SqlParameter("@IpAddress", SqlDbType.NVarChar, 20)
            {
                Value = string.IsNullOrWhiteSpace(ipAddress) ? DBNull.Value : ipAddress
            }
        };

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC [dbo].[I_SaveLabAdvanceAmount] " +
                    "@hospId, @branchId, @userId, @ClientID, @DepositDate, @PaymentMode, @PaymentModeId, @PaidAmount, " +
                    "@ChequeCardNo, @ChequeCardDate, @PaymentBankId, @PayMode, @TransactionId, @remarks, @IpAddress",
                    spParams
                );

                var created = await _context.LabAdvanceAmounts
                    .AsNoTracking()
                    .OrderByDescending(x => x.LabReceiptID)
                    .FirstOrDefaultAsync(x =>
                        x.HospId == hospId &&
                        x.BranchId == branchId &&
                        x.ClientID == clientId &&
                        x.TransactionId == paymentIdValue
                    );

                return Ok(new
                {
                    status = true,
                    message = "Payment successful",
                    data = new
                    {
                        orderId = orderIdValue,
                        paymentId = paymentIdValue,
                        receiptNo = created?.LabReceiptNo,
                        labAdvanceAmountId = created?.LabReceiptID
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = ex.Message,
                    data = (object?)null
                });
            }
        }

        // ===========================
        // SIGNATURE METHODS
        // ===========================
        private static string ComputeHmacSha256Hex(string secret, string message)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(messageBytes);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        private static bool FixedTimeEqualsHex(string expected, string actual)
        {
            var e = expected.ToLower();
            var a = actual.ToLower();

            if (e.Length != a.Length) return false;

            var eb = Convert.FromHexString(e);
            var ab = Convert.FromHexString(a);

            return CryptographicOperations.FixedTimeEquals(eb, ab);
        }
    }

    // ===========================
    // REQUEST MODELS
    // ===========================
    public class CreateOrderRequest
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int ClientId { get; set; }
        public int PaymentModeId { get; set; }
        public int CreatedBy { get; set; }
        public decimal Amount { get; set; }
    }

    public class VerifyPaymentRequest
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int ClientId { get; set; }
        public int PaymentModeId { get; set; }
        public int CreatedBy { get; set; }
        public decimal Amount { get; set; }
        public string? Remarks { get; set; }

        public string? razorpay_order_id { get; set; }
        public string? razorpay_payment_id { get; set; }
        public string? razorpay_signature { get; set; }

        public string GetOrderId() => razorpay_order_id!;
        public string GetPaymentId() => razorpay_payment_id!;
        public string GetSignature() => razorpay_signature!;
    }
}