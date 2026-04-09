using App.Data;
using App.Models;
using App.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
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
        private readonly ILogger<PaymentController> _logger;
        private readonly IHostEnvironment _env;

        public PaymentController(
            AppDbContext context,
            IOptions<RazorpaySettings> razorpaySettings,
            ILogger<PaymentController> logger,
            IHostEnvironment env)
        {
            _context = context;
            _razorpaySettings = razorpaySettings.Value;
            _logger = logger;
            _env = env;
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { message = "Payment API working" });
        }

        [HttpGet("check-razorpay")]
        public IActionResult CheckRazorpay()
        {
            var razorpayCheck = ValidateRazorpayConfig();
            if (!razorpayCheck.IsValid)
                return razorpayCheck.ErrorResult!;

            return Ok(new
            {
                message = "Razorpay configuration is valid.",
                key = razorpayCheck.Key
            });
        }

        [HttpPost("create-order")]
        [Consumes("application/json")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest? request)
        {
            if (request is null)
                return BadRequest(new { message = "Invalid or missing JSON body." });

            if (request.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0." });

            if (request.HospId <= 0 ||
                request.BranchId <= 0 ||
                request.ClientId <= 0 ||
                request.PaymentModeId <= 0 ||
                request.CreatedBy <= 0)
            {
                return BadRequest(new
                {
                    message = "HospId, BranchId, ClientId, PaymentModeId and CreatedBy must be greater than 0."
                });
            }

            var amountPaise = Convert.ToInt64(
                decimal.Round(request.Amount * 100m, 0, MidpointRounding.AwayFromZero)
            );

            if (amountPaise <= 0)
                return BadRequest(new { message = "Amount is too small." });

            if (amountPaise > int.MaxValue)
                return BadRequest(new { message = "Amount is too large." });

            var razorpayCheck = ValidateRazorpayConfig();
            if (!razorpayCheck.IsValid)
                return razorpayCheck.ErrorResult!;

            var key = razorpayCheck.Key!;
            var secret = razorpayCheck.Secret!;

            try
            {
                var client = new RazorpayClient(key, secret);

                var currency = string.IsNullOrWhiteSpace(request.Currency)
                    ? "INR"
                    : request.Currency.Trim().ToUpperInvariant();

                var receipt = string.IsNullOrWhiteSpace(request.Receipt)
                    ? Guid.NewGuid().ToString()
                    : request.Receipt.Trim();

                var options = new Dictionary<string, object>
                {
                    { "amount", (int)amountPaise },
                    { "currency", currency },
                    { "receipt", receipt }
                };

                var order = client.Order.Create(options);
                string? orderId = order["id"]?.ToString();

                if (string.IsNullOrWhiteSpace(orderId))
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        message = "Razorpay did not return an order id."
                    });
                }

                var orderIdNormalized = orderId.Trim();

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Length > 20)
                    ipAddress = ipAddress.Substring(0, 20);

                // Save pending advance amount via stored procedure (not EF entity insert).
                // SP generates LabReceiptNo via getBranchSequenceNumber and sets status='Pending', statusId=0.
                var spParams = new[]
                {
                    new SqlParameter("@hospId", SqlDbType.Int) { Value = request.HospId },
                    new SqlParameter("@branchId", SqlDbType.Int) { Value = request.BranchId },
                    new SqlParameter("@userId", SqlDbType.Int) { Value = request.CreatedBy },
                    new SqlParameter("@ClientID", SqlDbType.Int) { Value = request.ClientId },
                    new SqlParameter("@DepositDate", SqlDbType.Date) { Value = DateTime.UtcNow.Date },
                    new SqlParameter("@PaymentMode", SqlDbType.NVarChar, 50)
                    {
                        Value = string.IsNullOrWhiteSpace(request.PaymentMode) ? (object)DBNull.Value : request.PaymentMode.Trim()
                    },
                    new SqlParameter("@PaymentModeId", SqlDbType.Int) { Value = request.PaymentModeId },
                    new SqlParameter("@PaidAmount", SqlDbType.Decimal)
                    {
                        Precision = 16,
                        Scale = 6,
                        Value = request.Amount
                    },
                    // Store Razorpay order id for later verification/webhook lookup.
                    new SqlParameter("@ChequeCardNo", SqlDbType.NVarChar, 50) { Value = orderIdNormalized },
                    new SqlParameter("@ChequeCardDate", SqlDbType.Date) { Value = DBNull.Value },
                    new SqlParameter("@PaymentBankId", SqlDbType.Int) { Value = 0 },
                    new SqlParameter("@PayMode", SqlDbType.NVarChar, 20) { Value = "Razorpay" },
                    new SqlParameter("@TransactionId", SqlDbType.NVarChar, 50) { Value = DBNull.Value },
                    new SqlParameter("@remarks", SqlDbType.NVarChar, 512)
                    {
                        Value = string.IsNullOrWhiteSpace(request.Remarks) ? (object)DBNull.Value : request.Remarks.Trim()
                    },
                    new SqlParameter("@IpAddress", SqlDbType.NVarChar, 20)
                    {
                        Value = string.IsNullOrWhiteSpace(ipAddress) ? (object)DBNull.Value : ipAddress
                    }
                };

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC [dbo].[I_SaveLabAdvanceAmount] " +
                    "@hospId, @branchId, @userId, @ClientID, @DepositDate, @PaymentMode, @PaymentModeId, @PaidAmount, " +
                    "@ChequeCardNo, @ChequeCardDate, @PaymentBankId, @PayMode, @TransactionId, @remarks, @IpAddress",
                    spParams);

                // Read back the inserted row to return LabReceiptNo and identity.
                var created = await _context.LabAdvanceAmounts
                    .AsNoTracking()
                    .OrderByDescending(x => x.LabReceiptID)
                    .FirstOrDefaultAsync(x =>
                        x.HospId == request.HospId &&
                        x.BranchId == request.BranchId &&
                        x.ClientID == request.ClientId &&
                        x.ChequeCardNo == orderIdNormalized &&
                        (x.Status == "Pending" || x.StatusId == 0));

                return Ok(new
                {
                    key = key,
                    orderId = orderIdNormalized,
                    labReceiptNo = created?.LabReceiptNo,
                    amount = (int)amountPaise,
                    currency = currency,
                    labAdvanceAmountId = created?.LabReceiptID
                });
            }
            catch (Razorpay.Api.Errors.BadRequestError ex) when (
                ex.Message.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ex, "Razorpay authentication failed while creating order.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Razorpay authentication failed. Check RazorpaySettings:Key and RazorpaySettings:Secret."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateOrder failed.");

                var response = new Dictionary<string, object?>
                {
                    ["message"] = "Create order failed."
                };

                if (_env.IsDevelopment())
                    response["detail"] = ex.Message;

                return StatusCode(StatusCodes.Status502BadGateway, response);
            }
        }

        [HttpPost("verify-payment")]
        [Consumes("application/json")]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest? request)
        {
            if (request is null)
                return BadRequest(new { message = "Invalid or missing JSON body." });

            var orderId = request.GetOrderId();
            var paymentId = request.GetPaymentId();
            var signature = request.GetSignature();

            if (string.IsNullOrWhiteSpace(orderId) ||
                string.IsNullOrWhiteSpace(paymentId) ||
                string.IsNullOrWhiteSpace(signature))
            {
                return BadRequest(new { message = "Missing required fields." });
            }

            if (string.IsNullOrWhiteSpace(_razorpaySettings.Secret))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Razorpay secret missing in configuration."
                });
            }

            var payload = $"{orderId}|{paymentId}";
            var expectedSignature = ComputeHmacSha256Hex(_razorpaySettings.Secret, payload);

            if (!FixedTimeEqualsHex(expectedSignature, signature))
                return Unauthorized(new { message = "Invalid signature." });

            var normalizedOrderId = orderId.Trim();
            var orderIdUpper = normalizedOrderId.ToUpperInvariant();

            var existing = await _context.LabAdvanceAmounts.FirstOrDefaultAsync(x =>
                // Prefer ChequeCardNo match (we temporarily store Razorpay order id here)
                x.ChequeCardNo == normalizedOrderId ||
                (x.ChequeCardNo != null && x.ChequeCardNo.ToUpper() == orderIdUpper) ||
                // Legacy support: older rows stored Razorpay order id in UniqueId (uppercased)
                x.UniqueId == orderIdUpper ||
                (x.UniqueId != null && x.UniqueId.ToUpper() == orderIdUpper));

            if (existing is null)
                return NotFound(new { message = "Order not found in database." });

            if (string.Equals(existing.Status, "Verified", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existing.Status, "Verify", StringComparison.OrdinalIgnoreCase) ||
                existing.StatusId == 1)
            {
                return Ok(new
                {
                    message = "Payment already verified.",
                    orderId = orderId,
                    labReceiptNo = existing.LabReceiptNo,
                    paymentId = existing.TransactionId
                });
            }

            existing.TransactionId = paymentId;
            existing.Status = "Verify";
            existing.StatusId = 1;
            existing.VerifyBy = existing.CreatedBy;
            existing.VerifyOn = DateTime.UtcNow;
            existing.LastModifiedBy = existing.CreatedBy;
            existing.LastModifiedOn = DateTime.UtcNow;
            existing.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            existing.PayMode = "OnlinePay";
            existing.PaymentMode = string.IsNullOrWhiteSpace(existing.PaymentMode) ? "ONLINE" : existing.PaymentMode;
            existing.PaymentBankId ??= 0;
            existing.ChequeCardNo = null;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Payment verified and saved successfully.",
                orderId = orderId,
                labReceiptNo = existing.LabReceiptNo,
                paymentId = existing.TransactionId
            });
        }

        // Backward-compatible route (some clients use /api/payment/verify)
        [HttpPost("verify")]
        [Consumes("application/json")]
        public Task<IActionResult> Verify([FromBody] VerifyPaymentRequest? request) =>
            VerifyPayment(request);

        [HttpPost("webhook")]
        public async Task<IActionResult> RazorpayWebhook()
        {
            var signatureHeader = Request.Headers["X-Razorpay-Signature"].ToString();

            if (string.IsNullOrWhiteSpace(signatureHeader))
                return BadRequest(new { message = "Missing X-Razorpay-Signature header." });

            if (string.IsNullOrWhiteSpace(_razorpaySettings.WebhookSecret))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "WebhookSecret missing in configuration."
                });
            }

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var expectedSignature = ComputeHmacSha256Hex(_razorpaySettings.WebhookSecret, body);

            if (!FixedTimeEqualsHex(expectedSignature, signatureHeader))
                return Unauthorized(new { message = "Invalid webhook signature." });

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var root = doc.RootElement;

                var eventType = root.TryGetProperty("event", out var eventProp)
                    ? eventProp.GetString()
                    : null;

                if (eventType is not ("payment.captured" or "payment.authorized"))
                    return Ok(new { message = "Webhook ignored." });

                if (!root.TryGetProperty("payload", out var payloadEl) ||
                    !payloadEl.TryGetProperty("payment", out var paymentEl) ||
                    !paymentEl.TryGetProperty("entity", out var entityEl))
                {
                    return BadRequest(new { message = "Invalid webhook payload." });
                }

                var orderId = entityEl.TryGetProperty("order_id", out var orderIdEl)
                    ? orderIdEl.GetString()
                    : null;

                var paymentId = entityEl.TryGetProperty("id", out var paymentIdEl)
                    ? paymentIdEl.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId))
                    return BadRequest(new { message = "Missing order_id or payment id." });

                var normalizedOrderId = orderId.Trim();
                var orderIdUpper = normalizedOrderId.ToUpperInvariant();

                var existing = await _context.LabAdvanceAmounts.FirstOrDefaultAsync(x =>
                    x.ChequeCardNo == normalizedOrderId ||
                    (x.ChequeCardNo != null && x.ChequeCardNo.ToUpper() == orderIdUpper) ||
                    // Legacy support: older rows stored Razorpay order id in UniqueId (uppercased)
                    x.UniqueId == orderIdUpper ||
                    (x.UniqueId != null && x.UniqueId.ToUpper() == orderIdUpper));

                if (existing is null)
                {
                    _logger.LogWarning("Webhook received for unknown orderId {OrderId}", orderId);
                    return Ok(new { message = "Webhook processed." });
                }

                existing.TransactionId = paymentId;
                existing.Status = eventType == "payment.captured" ? "Verify" : "Authorized";
                existing.StatusId = eventType == "payment.captured" ? 1 : existing.StatusId;
                existing.VerifyBy = eventType == "payment.captured" ? existing.CreatedBy : existing.VerifyBy;
                existing.VerifyOn = eventType == "payment.captured" ? DateTime.UtcNow : existing.VerifyOn;
                existing.LastModifiedBy = existing.CreatedBy;
                existing.LastModifiedOn = DateTime.UtcNow;
                existing.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                existing.PayMode = "Razorpay";
                existing.PaymentMode = string.IsNullOrWhiteSpace(existing.PaymentMode) ? "Online" : existing.PaymentMode;
                existing.PaymentBankId ??= 0;
                if (eventType == "payment.captured") existing.ChequeCardNo = null;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Webhook processed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing failed.");
                return BadRequest(new
                {
                    message = "Webhook processing failed.",
                    detail = ex.Message
                });
            }
        }

        private (bool IsValid, IActionResult? ErrorResult, string? Key, string? Secret) ValidateRazorpayConfig()
        {
            var key = _razorpaySettings.Key?.Trim();
            var secret = _razorpaySettings.Secret?.Trim();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            {
                return (
                    false,
                    StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "Razorpay credentials missing in configuration."
                    }),
                    null,
                    null
                );
            }

            return (true, null, key, secret);
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

        private static bool FixedTimeEqualsHex(string expectedHexLower, string actualHex)
        {
            if (string.IsNullOrWhiteSpace(expectedHexLower) || string.IsNullOrWhiteSpace(actualHex))
                return false;

            var expected = expectedHexLower.Trim().ToLowerInvariant();
            var actual = actualHex.Trim().ToLowerInvariant();

            if (expected.Length != actual.Length)
                return false;

            try
            {
                var expectedBytes = Convert.FromHexString(expected);
                var actualBytes = Convert.FromHexString(actual);
                return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
            }
            catch
            {
                return false;
            }
        }

        private static string BuildLabReceiptNo(int labReceiptId, DateTimeOffset nowUtc)
        {
            // Financial year in India is Apr -> Mar.
            // Convert to IST when possible, otherwise treat UTC as local.
            var localNow = nowUtc;
            try
            {
                // Windows: "India Standard Time", Linux/macOS: "Asia/Kolkata"
                TimeZoneInfo ist;
                try
                {
                    ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                }
                catch
                {
                    ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                }

                localNow = TimeZoneInfo.ConvertTime(nowUtc, ist);
            }
            catch
            {
                // ignore TZ conversion failures
            }

            var year = localNow.Year;
            var fyStart = localNow.Month >= 4 ? year : year - 1;
            var fyEnd = fyStart + 1;

            var fySegment = $"{fyStart % 100:00}-{fyEnd % 100:00}";
            var seq = $"{labReceiptId:000000}";
            return $"CA/{fySegment}/{seq}";
        }
    }
}
