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
using System.Net.Http.Headers;
using System.Text.Json;

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
                orderId = order["id"]?.ToString(),
                amount = amount
            });
        }

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

                var result = await SaveOrUpdateVerifiedPaymentAsync(
                    new SavePaymentRequest
                    {
                        HospId = request.HospId,
                        BranchId = request.BranchId,
                        ClientId = request.ClientId,
                        PaymentModeId = request.PaymentModeId,
                        CreatedBy = request.CreatedBy,
                        Remarks = request.Remarks,
                        OrderId = razorpayOrderId,
                        PaymentId = razorpayPaymentId,
                        PaidAmount = request.Amount > 0 ? request.Amount : razorpayAmount
                    });

                return Ok(new
                {
                    status = true,
                    message = "Payment successful",
                    data = result
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

        // NEW ENDPOINT:
        // Call this when frontend gets cancel/error in UPI flow.
        [HttpPost("check-order-status")]
        public async Task<IActionResult> CheckOrderStatus([FromBody] CheckOrderStatusRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.OrderId))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "OrderId is required",
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

                if (request.HospId <= 0 || request.BranchId <= 0 || request.ClientId <= 0 ||
                    request.CreatedBy <= 0 || request.PaymentModeId <= 0)
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "HospId, BranchId, ClientId, CreatedBy and PaymentModeId must be greater than 0",
                        data = (object?)null
                    });
                }

                var paymentInfo = await FetchSuccessfulPaymentByOrderAsync(request.OrderId.Trim());

                if (paymentInfo == null)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "Payment not completed yet for this order",
                        data = new
                        {
                            orderId = request.OrderId,
                            paymentStatus = "pending"
                        }
                    });
                }

                var result = await SaveOrUpdateVerifiedPaymentAsync(
                    new SavePaymentRequest
                    {
                        HospId = request.HospId,
                        BranchId = request.BranchId,
                        ClientId = request.ClientId,
                        PaymentModeId = request.PaymentModeId,
                        CreatedBy = request.CreatedBy,
                        Remarks = request.Remarks,
                        OrderId = paymentInfo.OrderId,
                        PaymentId = paymentInfo.PaymentId,
                        PaidAmount = paymentInfo.Amount
                    });

                return Ok(new
                {
                    status = true,
                    message = "Payment confirmed from order status",
                    data = result
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

        private async Task<OrderPaymentInfo?> FetchSuccessfulPaymentByOrderAsync(string orderId)
        {
            using var httpClient = new HttpClient();

            var authValue = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_razorpaySettings.Key}:{_razorpaySettings.Secret}")
            );

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);

            var url = $"https://api.razorpay.com/v1/orders/{orderId}/payments";
            var response = await httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to fetch order payments from Razorpay. Response: {json}");
            }

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() == 0)
            {
                return null;
            }

            OrderPaymentInfo? latestSuccess = null;
            long latestCreatedAt = 0;

            foreach (var item in items.EnumerateArray())
            {
                var status = item.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()?.Trim()?.ToLower()
                    : "";

                if (status != "authorized" && status != "captured")
                    continue;

                var paymentId = item.TryGetProperty("id", out var idProp)
                    ? idProp.GetString()
                    : null;

                var orderIdValue = item.TryGetProperty("order_id", out var orderIdProp)
                    ? orderIdProp.GetString()
                    : orderId;

                long createdAt = item.TryGetProperty("created_at", out var createdAtProp) &&
                                 createdAtProp.ValueKind == JsonValueKind.Number
                    ? createdAtProp.GetInt64()
                    : 0;

                decimal amount = 0;
                if (item.TryGetProperty("amount", out var amountProp) &&
                    amountProp.ValueKind == JsonValueKind.Number)
                {
                    amount = amountProp.GetDecimal() / 100m;
                }

                if (createdAt >= latestCreatedAt && !string.IsNullOrWhiteSpace(paymentId))
                {
                    latestCreatedAt = createdAt;
                    latestSuccess = new OrderPaymentInfo
                    {
                        OrderId = orderIdValue ?? orderId,
                        PaymentId = paymentId!,
                        Amount = amount
                    };
                }
            }

            return latestSuccess;
        }

        private async Task<object> SaveOrUpdateVerifiedPaymentAsync(SavePaymentRequest request)
        {
            string orderIdValue = request.OrderId.Trim();
            string paymentIdValue = request.PaymentId.Trim();
            decimal paidAmount = request.PaidAmount;

            var indiaNow = GetIndiaNow();
            var indiaNowFormatted = FormatIndiaDateTime(indiaNow);

            var existing = await _context.LabAdvanceAmounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TransactionId == paymentIdValue);

            if (existing != null)
            {
                await UpdateVerifiedPaymentAsync(paymentIdValue, request.CreatedBy, indiaNow);

                var existingUpdated = await _context.LabAdvanceAmounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TransactionId == paymentIdValue);

                return new
                {
                    orderId = orderIdValue,
                    paymentId = paymentIdValue,
                    receiptNo = existingUpdated?.LabReceiptNo,
                    labAdvanceAmountId = existingUpdated?.LabReceiptID,
                    paymentDate = indiaNow.ToString("yyyy-MM-dd"),
                    paymentDateTime = indiaNowFormatted,
                    paymentStatus = "Verify",
                    paymentStatusId = 1,
                    verifyBy = request.CreatedBy,
                    verifyOn = indiaNowFormatted,
                    lastModifiedBy = request.CreatedBy,
                    lastModifiedOn = indiaNowFormatted,
                    createdOn = indiaNowFormatted
                };
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Length > 20)
                ipAddress = ipAddress.Substring(0, 20);

            var spParams = new[]
            {
                new SqlParameter("@hospId", SqlDbType.Int) { Value = request.HospId },
                new SqlParameter("@branchId", SqlDbType.Int) { Value = request.BranchId },
                new SqlParameter("@userId", SqlDbType.Int) { Value = request.CreatedBy },
                new SqlParameter("@ClientID", SqlDbType.Int) { Value = request.ClientId },
                new SqlParameter("@DepositDate", SqlDbType.Date) { Value = indiaNow.Date },
                new SqlParameter("@PaymentMode", SqlDbType.NVarChar, 50) { Value = "Online" },
                new SqlParameter("@PaymentModeId", SqlDbType.Int) { Value = request.PaymentModeId },
                new SqlParameter("@PaidAmount", SqlDbType.Decimal)
                {
                    Precision = 16,
                    Scale = 6,
                    Value = paidAmount
                },
                new SqlParameter("@ChequeCardNo", SqlDbType.NVarChar, 50) { Value = orderIdValue },
                new SqlParameter("@ChequeCardDate", SqlDbType.Date) { Value = DBNull.Value },
                new SqlParameter("@PaymentBankId", SqlDbType.Int) { Value = 0 },
                new SqlParameter("@PayMode", SqlDbType.NVarChar, 20) { Value = "OnlinePay" },
                new SqlParameter("@TransactionId", SqlDbType.NVarChar, 50) { Value = paymentIdValue },
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

            await UpdateVerifiedPaymentAsync(paymentIdValue, request.CreatedBy, indiaNow);

            var created = await _context.LabAdvanceAmounts
                .AsNoTracking()
                .OrderByDescending(x => x.LabReceiptID)
                .FirstOrDefaultAsync(x =>
                    x.HospId == request.HospId &&
                    x.BranchId == request.BranchId &&
                    x.ClientID == request.ClientId &&
                    x.TransactionId == paymentIdValue
                );

            return new
            {
                orderId = orderIdValue,
                paymentId = paymentIdValue,
                receiptNo = created?.LabReceiptNo,
                labAdvanceAmountId = created?.LabReceiptID,
                paymentDate = indiaNow.ToString("yyyy-MM-dd"),
                paymentDateTime = indiaNowFormatted,
                paymentStatus = "Verify",
                paymentStatusId = 1,
                verifyBy = request.CreatedBy,
                verifyOn = indiaNowFormatted,
                lastModifiedBy = request.CreatedBy,
                lastModifiedOn = indiaNowFormatted,
                createdOn = indiaNowFormatted
            };
        }

        private async Task UpdateVerifiedPaymentAsync(string transactionId, int userId, DateTime indiaNow)
        {
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE LabAdvanceAmount
                  SET
                      status = @status,
                      statusId = @statusId,
                      VerifyBy = @verifyBy,
                      VerifyOn = @verifyOn,
                      LastModifiedBy = @lastModifiedBy,
                      LastModifiedOn = @lastModifiedOn,
                      CreatedOn = @createdOn
                  WHERE TransactionId = @transactionId",
                new SqlParameter("@status", SqlDbType.NVarChar, 50) { Value = "Verify" },
                new SqlParameter("@statusId", SqlDbType.Int) { Value = 1 },
                new SqlParameter("@verifyBy", SqlDbType.Int) { Value = userId },
                new SqlParameter("@verifyOn", SqlDbType.DateTime) { Value = indiaNow },
                new SqlParameter("@lastModifiedBy", SqlDbType.Int) { Value = userId },
                new SqlParameter("@lastModifiedOn", SqlDbType.DateTime) { Value = indiaNow },
                new SqlParameter("@createdOn", SqlDbType.DateTime) { Value = indiaNow },
                new SqlParameter("@transactionId", SqlDbType.NVarChar, 50) { Value = transactionId }
            );
        }

        private static DateTime GetIndiaNow()
        {
            var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);
        }

        private static string FormatIndiaDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd hh:mm:ss tt");
        }

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

    public class CheckOrderStatusRequest
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int ClientId { get; set; }
        public int PaymentModeId { get; set; }
        public int CreatedBy { get; set; }
        public decimal Amount { get; set; }
        public string? Remarks { get; set; }
        public string OrderId { get; set; } = string.Empty;
    }

    public class SavePaymentRequest
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int ClientId { get; set; }
        public int PaymentModeId { get; set; }
        public int CreatedBy { get; set; }
        public string? Remarks { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
    }

    public class OrderPaymentInfo
    {
        public string OrderId { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}