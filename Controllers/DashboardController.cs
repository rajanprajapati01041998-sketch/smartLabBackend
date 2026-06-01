using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using log4net;
using iText.Html2pdf;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Runtime.InteropServices.JavaScript;
using iText.Kernel.Events;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.IO.Font.Constants;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;


namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(typeof(DashboardController));


        public DashboardController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("states")]
        public async Task<IActionResult> GetDashboardStates(
            int branchId,
            int userId,
            int roleId = 0,
            string clientIdList = "",
            string fromDate = "",
            string toDate = "")
        {
            try
            {
                _log.Info($"GetDashboardStates API called. branchId={branchId}, userId={userId}");

                using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand("S_DashBoard_States", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@roleId", roleId);
                cmd.Parameters.AddWithValue("@clientIdList", clientIdList);
                cmd.Parameters.AddWithValue("@fromDate", fromDate);
                cmd.Parameters.AddWithValue("@toDate", toDate);

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var result = new
                    {
                        totalVisitedCount = reader["totalVisitedCount"],
                        totalCollection = reader["totalCollection"],
                        grandTotalCollection = reader["grandTotalCollection"],
                        totalHospitalCollection = reader["totalHospitalCollection"],
                        totalStoreCollection = reader["totalStoreCollection"],
                        totalSamplePending = reader["totalSamplePending"],
                        totalSampleCollected = reader["totalSampleCollected"],
                        totalResultsPending = reader["totalResultsPending"],
                        totalResultsDone = reader["totalResultsDone"],
                        todayPurchaseTotalInvoice = reader["todayPurchaseTotalInvoice"],
                        todayPurchaseTotalMRP = reader["todayPurchaseTotalMRP"],
                        totalStockOnTrade = reader["totalStockOnTrade"],
                        totalStockOnMRP = reader["totalStockOnMRP"]
                    };

                    return Ok(result);
                }
                _log.Info($"GetDashboardStates success.");

                return Ok(new { message = "No data found" });
            }
            catch (Exception ex)
            {
                _log.Error("Unhandled error in GetDashboardStates API", ex);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("wallet")]
        public async Task<IActionResult> GetWalletAmount(string clientIds)
        {
            try
            {
                _log.Info($"GetWalletAmount API called. clientIds={clientIds}");
                using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand("S_GetWelletAMTData", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@clientId",
                    string.IsNullOrEmpty(clientIds) ? DBNull.Value : clientIds);

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var result = new
                    {
                        totalCredit = reader["TotalCredit"],
                        totalDebit = reader["TotalDebit"],
                        balance = reader["Balance"],
                        balanceMain = reader["BalanceMain"],
                        balanceMainDashboard = reader["BalanceMainDashboard"],
                        isDebitGreater = reader["IsDebitGreater"]
                    };

                    return Ok(result);
                }
                _log.Info($"GetWalletAmount success.");
                return Ok(new { message = "No data found" });
            }
            catch (Exception ex)
            {
                _log.Error("Unhandled error in GetWalletAmount API", ex);
                return StatusCode(500, ex.Message);
            }
        }


        [HttpGet("bill-advance")]
        public async Task<IActionResult> GetBillAndAdvanceDash(
            string? filter = null,
            string clientIdList = "",
            string? fromDate = null,
            string? toDate = null)
        {
            try
            {
                _log.Info($"GetBillAndAdvanceDash API called. filter={filter}, clientIdList={clientIdList}, fromDate={fromDate}, toDate={toDate}");
                using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand("S_GetBillAndAdvanceDash", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@filter",
                    string.IsNullOrEmpty(filter) ? DBNull.Value : filter);

                cmd.Parameters.AddWithValue("@clientIdList", clientIdList);

                cmd.Parameters.AddWithValue("@fromDate",
                    string.IsNullOrEmpty(fromDate) ? DBNull.Value : Convert.ToDateTime(fromDate));

                cmd.Parameters.AddWithValue("@toDate",
                    string.IsNullOrEmpty(toDate) ? DBNull.Value : Convert.ToDateTime(toDate));

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                // 🔹 First Result Set (Transactions)
                var transactions = new List<object>();

                while (await reader.ReadAsync())
                {
                    transactions.Add(new
                    {
                        type = reader["Type"],
                        amount = reader["Amount"],
                        createdOn = reader["CreatedOn"],
                        description = reader["Description"],
                        clientId = reader["ClientID"]
                    });
                }

                // 🔹 Second Result Set (Summary)
                object summary = null;

                if (await reader.NextResultAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        summary = new
                        {
                            totalCredit = reader["TotalCredit"],
                            totalDebit = reader["TotalDebit"],
                            balance = reader["Balance"]
                        };
                    }
                }

                _log.Info($"GetBillAndAdvanceDash success. transactionsCount={transactions.Count}, summaryExists={summary != null}");
                return Ok(new
                {
                    transactions,
                    summary
                });
            }
            catch (Exception ex)
            {
                _log.Error("Unhandled error in GetBillAndAdvanceDash API", ex);
                return StatusCode(500, ex.Message);

            }
        }




        [HttpGet("download-bill-advance-pdf")]
        public async Task<IActionResult> DownloadBillAdvancePdf(
    [FromQuery] string? filter,
    [FromQuery] string clientIdList,
    [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate,
    [FromQuery] int? userId,
    [FromQuery] bool colorPrint = false)
        {
            try
            {
                var details = new List<Dictionary<string, object?>>();
                var summary = new Dictionary<string, object?>();
                var branchSummary = new List<Dictionary<string, object?>>();
                string userName = "";

                using SqlConnection con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                await con.OpenAsync();

                if (userId.HasValue && userId.Value > 0)
                {
                    using SqlCommand userCmd = new SqlCommand(
                        "SELECT TOP 1 Name FROM UserMaster WHERE ID = @UserId",
                        con);

                    userCmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId.Value;

                    var userResult = await userCmd.ExecuteScalarAsync();
                    userName = userResult?.ToString() ?? "";
                }

                using SqlCommand cmd = new SqlCommand(
                    "S_GetBillAndAdvanceDashBranchWiseApp",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@filter", SqlDbType.NVarChar, 50).Value =
                    string.IsNullOrWhiteSpace(filter) ? DBNull.Value : filter;

                cmd.Parameters.Add("@clientIdList", SqlDbType.NVarChar).Value =
                    string.IsNullOrWhiteSpace(clientIdList) ? DBNull.Value : clientIdList;

                cmd.Parameters.Add("@fromDate", SqlDbType.Date).Value =
                    fromDate.HasValue ? fromDate.Value.Date : DBNull.Value;

                cmd.Parameters.Add("@toDate", SqlDbType.Date).Value =
                    toDate.HasValue ? toDate.Value.Date : DBNull.Value;

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                // Result Set 1: Details
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                    }

                    details.Add(row);
                }

                // Result Set 2: Overall Summary
                if (await reader.NextResultAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            summary[reader.GetName(i)] = reader.IsDBNull(i)
                                ? 0
                                : reader.GetValue(i);
                        }
                    }
                }


                // Result Set 3: Branch Wise Summary
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i)
                                ? null
                                : reader.GetValue(i);
                        }

                        branchSummary.Add(row);
                    }
                }

                string templatePath = System.IO.Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Templates",
                    "BillAdvanceReport.html");

                if (!System.IO.File.Exists(templatePath))
                {
                    return StatusCode(500, new
                    {
                        status = false,
                        message = "HTML template not found",
                        path = templatePath
                    });
                }

                string html = await System.IO.File.ReadAllTextAsync(templatePath);

                var rows = new StringBuilder();

                var groupedBranches = details
                    .GroupBy(x => x.GetValueOrDefault("BranchName")?.ToString() ?? "Unknown Branch")
                    .OrderBy(x => x.Key)
                    .ToList();

                int srNo = 1;

                string branchHeaderBg = colorPrint ? "#1e3a8a" : "#000000";
                string branchHeaderText = "#ffffff";
                foreach (var branch in groupedBranches)
                {
                    rows.Append($@"
                    <tr>
                        <td colspan='5'
                            style='background:{branchHeaderBg};
                                color:{branchHeaderText};
                                font-size:17px;
                                font-weight:bold;
                                padding:10px;
                                text-align:left;'>
                            {srNo}. {branch.Key}
                        </td>
                    </tr>");



                    foreach (var item in branch)
                    {
                        string type = item.GetValueOrDefault("Type")?.ToString() ?? "";

                        string rowClass = type.ToLower() switch
                        {
                            "credit" => "credit-row",
                            "debit" => "debit-row",
                            "refund" => "refund-row",
                            _ => ""
                        };

                        decimal amount = 0;
                        decimal.TryParse(item.GetValueOrDefault("Amount")?.ToString(), out amount);

                        rows.Append($@"
                        <tr class='{rowClass}'>
                            <td>{type}</td>
                            <td style='text-align:right'>{amount:N2}</td>
                            <td>{item.GetValueOrDefault("CreatedOn")}</td>
                            <td>{item.GetValueOrDefault("Description")}</td>
                            <td>{item.GetValueOrDefault("BranchName")}</td>
                        </tr>");
                    }

                    var currentBranchSummary = branchSummary.FirstOrDefault(x =>
                        x.GetValueOrDefault("BranchName")?.ToString() == branch.Key);

                    if (currentBranchSummary != null)
                    {
                        decimal branchCredit = 0;
                        decimal branchDebit = 0;
                        decimal branchRefund = 0;
                        decimal branchBalance = 0;

                        decimal.TryParse(currentBranchSummary.GetValueOrDefault("TotalCredit")?.ToString(), out branchCredit);
                        decimal.TryParse(currentBranchSummary.GetValueOrDefault("TotalDebit")?.ToString(), out branchDebit);
                        decimal.TryParse(currentBranchSummary.GetValueOrDefault("TotalRefund")?.ToString(), out branchRefund);
                        decimal.TryParse(currentBranchSummary.GetValueOrDefault("Balance")?.ToString(), out branchBalance);

                        rows.Append($@"
                        <tr>
                            <td colspan='5' style='padding:0;border:none;'>
                                <table style='width:100%;margin-top:8px;margin-bottom:18px;border-collapse:collapse;'>
                                    <tr>
                                        <th colspan='4' style='background:#111827;color:white;text-align:center;padding:8px;'>
                                            Branch Summary - {branch.Key}
                                        </th>
                                    </tr>
                                    <tr>
                                        <th>Total Credit</th>
                                        <th>Total Debit</th>
                                        <th>Total Refund</th>
                                        <th>Balance</th>
                                    </tr>
                                    <tr>
                                        <td class='credit' style='text-align:right;font-weight:bold;'>{branchCredit:N2}</td>
                                        <td class='debit' style='text-align:right;font-weight:bold;'>{branchDebit:N2}</td>
                                        <td class='refund' style='text-align:right;font-weight:bold;'>{branchRefund:N2}</td>
                                        <td class='balance' style='text-align:right;font-weight:bold;'>{branchBalance:N2}</td>
                                    </tr>
                                </table>
                            </td>
                        </tr>");
                    }

                    rows.Append(@"
                    <tr>
                        <td colspan='5' style='height:15px;border:none;background:white;'></td>
                    </tr>");

                    srNo++;
                }

                html = html
                    .Replace("{{COLOR_MODE}}", colorPrint ? "color-mode" : "bw-mode")
                    .Replace("{{USER_NAME}}", string.IsNullOrWhiteSpace(userName) ? "-" : userName)
                    .Replace("{{FROM_DATE}}", fromDate?.ToString("dd-MM-yyyy") ?? "-")
                    .Replace("{{TO_DATE}}", toDate?.ToString("dd-MM-yyyy") ?? "-")
                    .Replace("{{PRINT_DATE_TIME}}", DateTime.Now.ToString("dd-MM-yyyy hh:mm tt"))
                    .Replace("{{ROWS}}", rows.ToString())
                    .Replace("{{TOTAL_CREDIT}}", summary.GetValueOrDefault("TotalCredit")?.ToString() ?? "0")
                    .Replace("{{TOTAL_DEBIT}}", summary.GetValueOrDefault("TotalDebit")?.ToString() ?? "0")
                    .Replace("{{TOTAL_REFUND}}", summary.GetValueOrDefault("TotalRefund")?.ToString() ?? "0")
                    .Replace("{{BALANCE}}", summary.GetValueOrDefault("Balance")?.ToString() ?? "0");

                using MemoryStream ms = new MemoryStream();

                HtmlConverter.ConvertToPdf(html, ms);

                byte[] pdfBytesWithNumbers = AddPageNumbers(ms.ToArray());

                string fileName = $"BillAdvanceReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                return File(pdfBytesWithNumbers, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "PDF generation failed",
                    error = ex.Message
                });
            }
        }

        private static byte[] AddPageNumbers(byte[] inputPdfBytes)
        {
            using MemoryStream input = new MemoryStream(inputPdfBytes);
            using PdfReader reader = new PdfReader(input, new ReaderProperties());
            using MemoryStream output = new MemoryStream();
            using PdfWriter writer = new PdfWriter(output);
            using PdfDocument pdfDocument = new PdfDocument(reader, writer);

            int totalPages = pdfDocument.GetNumberOfPages();
            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            for (int pageIndex = 1; pageIndex <= totalPages; pageIndex++)
            {
                PdfPage page = pdfDocument.GetPage(pageIndex);
                Rectangle pageSize = page.GetPageSize();

                float y = pageSize.GetBottom() + 20;
                float x = pageSize.GetRight() - 20;

                PdfCanvas pdfCanvas = new PdfCanvas(
                    page.NewContentStreamAfter(),
                    page.GetResources(),
                    pdfDocument);

                using Canvas canvas = new Canvas(pdfCanvas, pageSize);

                canvas.SetFont(font).SetFontSize(9f);

                canvas.ShowTextAligned(
                    new Paragraph($"Page {pageIndex} of {totalPages}"),
                    x,
                    y,
                    TextAlignment.RIGHT);
            }

            pdfDocument.Close();

            return output.ToArray();
        }
    }
}
