using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using ZXing;
using ZXing.Common;
using Microsoft.AspNetCore.Authorization;

namespace LISDBACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptBillController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public ReceiptBillController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        // [Authorize]
        [HttpGet("details-bill")]
        public async Task<IActionResult> GetDetailsBill(
            [FromQuery] int ftId,
            [FromQuery] int receiptId,
            [FromQuery] int printUserId,
            [FromQuery] string isReceipt = "true",
            [FromQuery] string mode = "pdf"
        )
        {
            try
            {
                var receiptDetails = await GetReceiptDetails(ftId, isReceipt, receiptId, printUserId);

                if (receiptDetails.Count == 0)
                {
                    return NotFound(new
                    {
                        status = false,
                        message = "No receipt details found"
                    });
                }

                var first = receiptDetails.First();

                string receiptNo = GetString(first, "ReceiptNo");
                string visitId = GetString(first, "VisitId");

                var paymentDetails = await GetReceiptPaymentDetails(receiptNo);
                var opdReceiptDetails = await GetReceiptAllDetailsForOPDPatient(visitId);
                var previousReceiptAmount = await GetPreviousReceiptAmount(ftId);

                string html = BuildDetailsBillHtml(
                    receiptDetails,
                    paymentDetails,
                    opdReceiptDetails,
                    previousReceiptAmount
                );

                byte[] pdfBytes = GeneratePdfFromHtml(html);

                string requestMode = (mode ?? "pdf").Trim().ToLower();

                if (requestMode == "pdf")
                {
                    return File(pdfBytes, "application/pdf", $"DetailsBill_{ftId}.pdf");
                }

                if (requestMode == "view")
                {
                    string base64Pdf = Convert.ToBase64String(pdfBytes);

                    return Ok(new
                    {
                        status = true,
                        message = "PDF base64 generated successfully",
                        fileName = $"DetailsBill_{ftId}.pdf",
                        contentType = "application/pdf",
                        base64 = base64Pdf
                    });
                }

                return BadRequest(new
                {
                    status = false,
                    message = "Invalid mode. Use mode=pdf or mode=view"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        private async Task<List<Dictionary<string, object?>>> GetReceiptDetails(
            int ftId,
            string isReceipt,
            int receiptId,
            int printUserId)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@FTID", ftId),
                new SqlParameter("@isReceipt", isReceipt ?? "true"),
                new SqlParameter("@receiptId", receiptId),
                new SqlParameter("@printUserId", printUserId)
            };

            return await ExecuteStoredProcedure("S_GetReceiptDetails", parameters);
        }

        private async Task<List<Dictionary<string, object?>>> GetReceiptPaymentDetails(string receiptNo)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@ReceiptNo", receiptNo ?? "")
            };

            return await ExecuteStoredProcedure("S_GetReceiptPaymentDetails", parameters);
        }

        private async Task<List<Dictionary<string, object?>>> GetReceiptAllDetailsForOPDPatient(string visitNo)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@VisitNo", visitNo ?? "")
            };

            return await ExecuteStoredProcedure("S_GetReceiptAllDetailsForOPDPatient", parameters);
        }

        private async Task<List<Dictionary<string, object?>>> GetPreviousReceiptAmount(int ftId)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@FTID", ftId)
            };

            return await ExecuteStoredProcedure("S_GetPreviousReceiptAmount", parameters);
        }

        private async Task<List<Dictionary<string, object?>>> ExecuteStoredProcedure(
            string procedureName,
            SqlParameter[] parameters)
        {
            var result = new List<Dictionary<string, object?>>();

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new Exception("DefaultConnection is missing in appsettings.json");

            using SqlConnection con = new SqlConnection(connectionString);
            using SqlCommand cmd = new SqlCommand(procedureName, con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);

            await con.OpenAsync();

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                result.Add(row);
            }

            return result;
        }

        private byte[] GeneratePdfFromHtml(string html)
        {
            using MemoryStream ms = new MemoryStream();

            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdfDocument = new PdfDocument(writer);

            ConverterProperties properties = new ConverterProperties();
            properties.SetCharset("utf-8");
            properties.SetBaseUri(_env.ContentRootPath);

            HtmlConverter.ConvertToPdf(html, pdfDocument, properties);

            pdfDocument.Close();

            return ms.ToArray();
        }

        private string BuildDetailsBillHtml(
            List<Dictionary<string, object?>> receiptDetails,
            List<Dictionary<string, object?>> paymentDetails,
            List<Dictionary<string, object?>> opdReceiptDetails,
            List<Dictionary<string, object?>> previousReceiptAmount)
        {
            var first = receiptDetails.First();

            string templatePath = Path.Combine(
                _env.ContentRootPath,
                "Templates",
                "ReceiptBillTemplate.html"
            );

            if (!System.IO.File.Exists(templatePath))
                throw new Exception($"ReceiptBillTemplate.html not found at: {templatePath}");

            string html = System.IO.File.ReadAllText(templatePath);

            string headerHtml = GetString(first, "ReceiptHeader");
            headerHtml = FixReceiptHeaderHtml(headerHtml);

            html = html.Replace("{{HEADER_HTML}}", headerHtml);

            string patientName = GetString(first, "PatientName");
            string uhid = GetString(first, "UHID");
            string age = GetString(first, "Age");
            string gender = GetString(first, "Gender");
            string contactNo = GetString(first, "ContactNumber");
            string relativeName = GetString(first, "RelativeName");
            string address = GetString(first, "Address");
            string labNo = GetString(first, "DiagnosticNo");
            string billDate = GetString(first, "BillDate");
            string referDoctor = GetString(first, "ReferDoctorName");
            string corporate = GetString(first, "Corporat");
            string billNo = GetString(first, "BillNo");
            string receiptNo = GetString(first, "ReceiptNo");
            string createdBy = GetString(first, "CreatedBy");
            string printBy = GetString(first, "PrintBy");

            decimal grossAmount = GetDecimal(first, "GrossAmount");
            decimal discountAmount = GetDecimal(first, "DiscountAmount");
            decimal netAmount = GetDecimal(first, "NetAmount");
            decimal paidAmount = GetDecimal(first, "TotalPaidAmount");
            decimal balanceAmount = GetDecimal(first, "TotalBalanceAmount");

            string amountInWords = NumberToWords((long)netAmount) + " Only";

            var serviceRows = new StringBuilder();

            foreach (var item in receiptDetails)
            {
                serviceRows.Append($@"
<tr>
    <td>{Html(GetString(item, "ServiceName"))}</td>
    <td>{Html(GetString(item, "CorporateCode"))}</td>
    <td class='center'>{FormatAmount(GetDecimal(item, "Qty"))}</td>
    <td class='right'>{FormatAmount(GetDecimal(item, "Rate"))}</td>
    <td>{Html(GetString(item, "BillDate"))}</td>
    <td class='right'>{FormatAmount(GetDecimal(item, "NetAmt"))}</td>
</tr>");
            }

            var receiptRows = new StringBuilder();

            if (opdReceiptDetails.Count > 0)
            {
                foreach (var item in opdReceiptDetails)
                {
                    receiptRows.Append($@"
<tr>
    <td>{Html(GetString(item, "BillDate"))}</td>
    <td>{Html(GetString(item, "ReceiptNo"))}</td>
    <td class='right'>{FormatAmount(GetDecimal(item, "Amount"))}</td>
    <td>{Html(GetString(item, "PaymentModeName"))}</td>
    <td>{Html(GetString(item, "UserName"))}</td>
</tr>");
                }
            }
            else
            {
                foreach (var item in paymentDetails)
                {
                    receiptRows.Append($@"
<tr>
    <td>{Html(GetString(first, "ReceiptDate"))}</td>
    <td>{Html(receiptNo)}</td>
    <td class='right'>{FormatAmount(GetDecimal(item, "Amount"))}</td>
    <td>{Html(GetString(item, "PaymentModeName"))}</td>
    <td>{Html(createdBy)}</td>
</tr>");
                }
            }

            html = html.Replace("{{HEADER_HTML}}", headerHtml)
                       .Replace("{{UHID_BARCODE}}", BuildBarcodeImageHtml(uhid))
                       .Replace("{{BILL_BARCODE}}", BuildBarcodeImageHtml(billNo))
                       .Replace("{{UHID}}", Html(uhid))
                       .Replace("{{PATIENT_NAME}}", Html(patientName))
                       .Replace("{{AGE_SEX}}", Html($"{age} {gender}"))
                       .Replace("{{CONTACT_NO}}", Html(contactNo))
                       .Replace("{{RELATIVE_NAME}}", Html(relativeName))
                       .Replace("{{ADDRESS}}", Html(address))
                       .Replace("{{LAB_NO}}", Html(labNo))
                       .Replace("{{BILL_DATE}}", Html(billDate))
                       .Replace("{{DOCTOR}}", "")
                       .Replace("{{REFER_DOCTOR}}", Html(referDoctor))
                       .Replace("{{CORPORATE}}", Html(corporate))
                       .Replace("{{BILL_NO}}", Html(billNo))
                       .Replace("{{RECEIPT_NO}}", Html(receiptNo))
                       .Replace("{{TOTAL_AMOUNT}}", FormatAmount(grossAmount))
                       .Replace("{{TOTAL_DISCOUNT}}", FormatAmount(discountAmount))
                       .Replace("{{NET_AMOUNT}}", FormatAmount(netAmount))
                       .Replace("{{PAID_AMOUNT}}", FormatAmount(paidAmount))
                       .Replace("{{BALANCE_AMOUNT}}", FormatAmount(balanceAmount))
                       .Replace("{{AMOUNT_WORDS}}", Html(amountInWords))
                       .Replace("{{CREATED_BY}}", Html(createdBy))
                       .Replace("{{PRINT_BY}}", Html(printBy))
                       .Replace("{{SERVICE_ROWS}}", serviceRows.ToString())
                       .Replace("{{RECEIPT_ROWS}}", receiptRows.ToString());

            return html;
        }

        private static string FixReceiptHeaderHtml(string headerHtml)
        {
            if (string.IsNullOrWhiteSpace(headerHtml))
                return "";

            // ❌ Remove <p> wrapper completely
            headerHtml = headerHtml.Replace("<p>", "").Replace("</p>", "");

            // ❌ Remove float
            headerHtml = headerHtml.Replace("float:left;", "");
            headerHtml = headerHtml.Replace("float: left;", "");

            // ✅ Force full width
            headerHtml = headerHtml.Replace("height:120px", "width:100%; height:120px; object-fit:contain;");
            headerHtml = headerHtml.Replace("height: 120px", "width:100%; height:120px; object-fit:contain;");

            // ✅ Ensure image block behavior
            headerHtml = headerHtml.Replace("<img", "<img style='display:block; margin:0 auto;'");

            return headerHtml;
        }

        private static string BuildBarcodeImageHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            try
            {
                var writer = new BarcodeWriterSvg
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new EncodingOptions
                    {
                        Width = 320,
                        Height = 55,
                        Margin = 2,
                        PureBarcode = true
                    }
                };

                string svg = writer.Write(text).Content;
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

                return $"<img src='data:image/svg+xml;base64,{base64}' style='width:320px; height:55px; display:block; margin:0 auto;' />";
            }
            catch
            {
                return $"<div style='font-size:14px;font-weight:bold;text-align:center;'>{Html(text)}</div>";
            }
        }

        private static string GetString(Dictionary<string, object?> row, string key)
        {
            if (!row.ContainsKey(key) || row[key] == null || row[key] == DBNull.Value)
                return "";

            return Convert.ToString(row[key]) ?? "";
        }

        private static decimal GetDecimal(Dictionary<string, object?> row, string key)
        {
            if (!row.ContainsKey(key) || row[key] == null || row[key] == DBNull.Value)
                return 0;

            decimal.TryParse(Convert.ToString(row[key]), out decimal value);
            return value;
        }

        private static string FormatAmount(decimal amount)
        {
            return amount % 1 == 0 ? amount.ToString("0") : amount.ToString("0.00");
        }

        private static string Html(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? "");
        }

        private static string NumberToWords(long number)
        {
            if (number == 0)
                return "Zero";

            if (number < 0)
                return "Minus " + NumberToWords(Math.Abs(number));

            string words = "";

            if ((number / 10000000) > 0)
            {
                words += NumberToWords(number / 10000000) + " Crore ";
                number %= 10000000;
            }

            if ((number / 100000) > 0)
            {
                words += NumberToWords(number / 100000) + " Lakh ";
                number %= 100000;
            }

            if ((number / 1000) > 0)
            {
                words += NumberToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += NumberToWords(number / 100) + " Hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "and ";

                string[] unitsMap =
                {
                    "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven",
                    "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen",
                    "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen",
                    "Nineteen"
                };

                string[] tensMap =
                {
                    "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty",
                    "Sixty", "Seventy", "Eighty", "Ninety"
                };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];

                    if ((number % 10) > 0)
                        words += " " + unitsMap[number % 10];
                }
            }

            return words.Trim();
        }
    }
}