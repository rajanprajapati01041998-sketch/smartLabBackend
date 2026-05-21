using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Html2pdf;
using QRCoder;
using Path = System.IO.Path;

namespace App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportPrintController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ReportPrintController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("DownloadCombinedReport")]
        public IActionResult DownloadCombinedReport(
            int ptInvstId,
            int isHeaderPNG = 0,
            string printBy = null,
            string branchId = null,
            bool pdf = true)
        {
            try
            {
                if (ptInvstId <= 0)
                    return BadRequest("ptInvstId must be provided");

                DataTable headerData = GetPatientInvestigations(
                    ptInvstId.ToString(),
                    isHeaderPNG,
                    printBy,
                    branchId
                );

                if (headerData.Rows.Count == 0)
                    return NotFound("No data found");

                string diagnosticsNo =
                    headerData.Rows[0]["LabNo"]?.ToString()
                    ?? ptInvstId.ToString(CultureInfo.InvariantCulture);

                string resultsHtml = GetFreeText(ptInvstId);
                if (string.IsNullOrWhiteSpace(resultsHtml))
                    resultsHtml = "<p>No results available.</p>";

                string doctorSignaturesHtml = GetDoctorSignatures(headerData);
                string currentDateTime = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt");
                string investigationNames = GetInvestigationNames(headerData);

                string qrUrl =
                    $"{Request.Scheme}://{Request.Host}/api/ReportPrint/DownloadCombinedReport?ptInvstId={ptInvstId}&isHeaderPNG={isHeaderPNG}&printBy={printBy}&branchId={branchId}&pdf=true";

                string qrBase64 = GenerateQr(qrUrl);
                string barcodeBase64 = GenerateBarcode(diagnosticsNo);

                string html = BuildHtml(
                    headerData.Rows[0],
                    resultsHtml,
                    qrBase64,
                    barcodeBase64,
                    doctorSignaturesHtml,
                    currentDateTime,
                    isHeaderPNG,
                    investigationNames
                );

                byte[] pdfBytes = ConvertToPdf(html);

                if (pdf)
                {
                    return File(
                        pdfBytes,
                        "application/pdf",
                        $"Report_{diagnosticsNo}.pdf"
                    );
                }

                return Ok(new
                {
                    status = true,
                    message = "Success",
                    fileName = $"Report_{diagnosticsNo}.pdf",
                    contentType = "application/pdf",
                    pdfBase64 = Convert.ToBase64String(pdfBytes)
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null
                    ? $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                    : "";

                return StatusCode(500, new
                {
                    status = false,
                    message = $"Error generating report: {ex.GetType().Name}: {ex.Message}{inner}"
                });
            }
        }

        private DataTable GetPatientInvestigations(
            string ids,
            int isHeaderPNG,
            string printBy,
            string branchId)
        {
            using var con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection")
            );

            using var cmd = new SqlCommand(
                "S_GetPatientInvestigationsForReportPrint",
                con
            );

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@PatientInvestigationIdList", ids);
            cmd.Parameters.AddWithValue("@isHeaderPNG", isHeaderPNG);
            cmd.Parameters.AddWithValue("@PrintBy", printBy ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);

            DataTable dt = new DataTable();

            con.Open();

            using var da = new SqlDataAdapter(cmd);
            da.Fill(dt);

            return dt;
        }

        private string GetFreeText(int id)
        {
            using var con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection")
            );

            using var cmd = new SqlCommand(
                "S_GetPatientFreeTextResultsForPrint",
                con
            );

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@PTInvstId", id);

            con.Open();

            var result = cmd.ExecuteScalar();

            return result?.ToString() ?? "<p>No results available.</p>";
        }

        private string GetDoctorSignatures(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
                return "";

            StringBuilder sb = new StringBuilder();

            foreach (DataRow row in dt.Rows)
            {
                string doctorName =
                    row.Table.Columns.Contains("InvReportApprovedBy") &&
                    row["InvReportApprovedBy"] != DBNull.Value
                        ? row["InvReportApprovedBy"].ToString()
                        : "";

                string signPath =
                    row.Table.Columns.Contains("DoctorSignFilePath") &&
                    row["DoctorSignFilePath"] != DBNull.Value
                        ? row["DoctorSignFilePath"].ToString()
                        : "";

                if (string.IsNullOrWhiteSpace(doctorName) &&
                    string.IsNullOrWhiteSpace(signPath))
                    continue;

                string imgTag = "";

                try
                {
                    string base64 = ImageToBase64(signPath);

                    if (!string.IsNullOrWhiteSpace(base64))
                    {
                        imgTag =
                            $"<img src='data:image/png;base64,{base64}' class='doctor-sign' />";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Doctor Sign Error: " + ex.Message);
                }

                sb.Append($@"
                    <div class='doctor-card'>
                        {imgTag}
                        <div class='signature-line'></div>
                        <div class='doctor-name'>{doctorName}</div>
                    </div>
                ");
            }

            return sb.ToString();
        }

        private string BuildHtml(
    DataRow row,
    string results,
    string qr,
    string barcode,
    string doctorSignatures,
    string currentDateTime,
    int isHeaderPNG,
    string investigationNames)
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Templates",
                "ReportTemplate.html"
            );

            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException($"Template file not found at: {path}");

            string html = System.IO.File.ReadAllText(path);

            string Get(string columnName)
            {
                return row.Table.Columns.Contains(columnName) &&
                       row[columnName] != DBNull.Value
                    ? row[columnName].ToString()
                    : "";
            }

            string headerSectionHtml = "";
            string topSpaceSectionHtml = "";

            if (isHeaderPNG == 1)
            {
                string letterHeadPath = Get("LetterHeadFilePath");

                if (!string.IsNullOrWhiteSpace(letterHeadPath))
                {
                    string headerBase64 = ImageToBase64(letterHeadPath);

                    if (!string.IsNullOrWhiteSpace(headerBase64))
                    {
                        headerSectionHtml = $@"
                <div class='report-header'>
                    <img src='data:image/png;base64,{headerBase64}' class='letter-head-img' />
                </div>";
                    }
                }
            }
            else
            {
                // When header is hidden, keep a fixed top gap (requested 150px via CSS).
                topSpaceSectionHtml = "<div class='top-empty-space'></div>";
            }

            string nablHtml = "";
            string nablPath = Get("NABLPath");

            if (!string.IsNullOrWhiteSpace(nablPath))
            {
                string nablBase64 = ImageToBase64(nablPath);

                if (!string.IsNullOrWhiteSpace(nablBase64))
                {
                    nablHtml = $@"
            <div class='nabl-wrapper'>
                <img src='data:image/png;base64,{nablBase64}' class='nabl-img' />
            </div>";
                }
            }

            html = html.Replace("{{HEADER_SECTION}}", headerSectionHtml);
            html = html.Replace("{{TOP_SPACE_SECTION}}", topSpaceSectionHtml);
            html = html.Replace("{{NABL_IMAGE}}", nablHtml);

            html = html.Replace("{{LabNo}}", Get("LabNo"));
            html = html.Replace("{{UHID}}", Get("UHID"));
            html = html.Replace("{{PName}}", Get("PName"));
            html = html.Replace("{{RegDate}}", Get("InvBillingDateTime"));
            html = html.Replace("{{AgeGender}}", $"{Get("Age")} / {Get("Gender")}");
            html = html.Replace("{{ContactNo}}", Get("ContactNo"));
            html = html.Replace("{{CollectionDate}}", Get("InvSampleColDateTime"));
            html = html.Replace("{{ReportDate}}", Get("InvReportApprovalDateTime"));
            html = html.Replace("{{ReferredBy}}", Get("InvReferdBy"));
            html = html.Replace("{{Status}}", Get("InvReportStatus"));
            html = html.Replace("{{SampleType}}", Get("SampleType"));
            html = html.Replace("{{ReferLab}}", Get("ReferLabName"));
            html = html.Replace("{{PreparedBy}}", Get("InvResultEntryBy"));
            html = html.Replace("{{PrintedBy}}", Get("InvResultPrintBy"));
            html = html.Replace("{{SubSubCategoryName}}", Get("SubSubCategoryName"));
            html = html.Replace("{{Results}}", results);
            html = html.Replace("{{InvestigationNames}}", investigationNames ?? "");
            html = html.Replace("{{DoctorSignatures}}", doctorSignatures);
            html = html.Replace("{{CurrentDateTime}}", currentDateTime);

            html = html.Replace("{{QR}}", $"data:image/png;base64,{qr}");
            html = html.Replace("{{BARCODE}}", $"data:image/svg+xml;base64,{barcode}");

            return html;
        }

        private static string GetInvestigationNames(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
                return "";

            // Stored proc column names can differ across deployments; try common variants.
            string[] candidates =
            {
                "InvestigationName",
                "Investigation",
                "InvName",
                "TestName",
                "InvestigationTitle",
                "SubSubCategoryName"
            };

            string col = candidates.FirstOrDefault(dt.Columns.Contains);
            if (string.IsNullOrWhiteSpace(col))
                return "";

            var distinct = dt.Rows
                .Cast<DataRow>()
                .Select(r => r[col] == DBNull.Value ? "" : r[col]?.ToString()?.Trim() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return distinct.Count == 0 ? "" : string.Join(", ", distinct);
        }

        private byte[] ConvertToPdf(string html)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);

            pdf.SetDefaultPageSize(PageSize.A4);

            var converterProperties = new ConverterProperties();
            converterProperties.SetBaseUri(Directory.GetCurrentDirectory());

            HtmlConverter.ConvertToPdf(html ?? string.Empty, pdf, converterProperties);

            pdf.Close();

            return ms.ToArray();
        }

        private string GenerateQr(string text)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);

            var qr = new PngByteQRCode(data);

            return Convert.ToBase64String(qr.GetGraphic(5));
        }

        private string ImageToBase64(string imagePathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(imagePathOrUrl))
                return "";

            try
            {
                if (
                    imagePathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    imagePathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                )
                {
                    using HttpClient client = new HttpClient();
                    byte[] bytes = client.GetByteArrayAsync(imagePathOrUrl).Result;
                    return Convert.ToBase64String(bytes);
                }

                if (System.IO.File.Exists(imagePathOrUrl))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(imagePathOrUrl);
                    return Convert.ToBase64String(bytes);
                }

                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Image Base64 Error: " + ex.Message);
                return "";
            }
        }

        private string GenerateBarcode(string text)
        {
            try
            {
                var writer = new ZXing.BarcodeWriterSvg
                {
                    Format = ZXing.BarcodeFormat.CODE_128,
                    Options = new ZXing.Common.EncodingOptions
                    {
                        Width = 280,
                        Height = 72,
                        Margin = 4,
                        PureBarcode = false
                    }
                };

                string doc = writer.Write(text).Content;

                if (!TryParseSvgBarcodeSize(doc, out double bw, out double bh))
                {
                    bw = 288;
                    bh = 72;
                }

                string inner = ExtractFirstSvgInnerXml(doc);

                if (inner.Length == 0)
                    inner = doc;

                string rotated = FormattableString.Invariant(
                    $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""{bh:0.##}"" height=""{bw:0.##}"" viewBox=""0 0 {bh:0.##} {bw:0.##}"">
                        <rect width=""100%"" height=""100%"" fill=""#ffffff""/>
                        <g transform=""translate({bh / 2:0.##},{bw / 2:0.##}) rotate(90) translate({-bw / 2:0.##},{-bh / 2:0.##})"">
                            {inner}
                        </g>
                    </svg>"
                );

                return Convert.ToBase64String(Encoding.UTF8.GetBytes(rotated));
            }
            catch (Exception ex)
            {

                string fallbackSvg = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                <svg xmlns=""http://www.w3.org/2000/svg"" width=""62"" height=""288"" viewBox=""0 0 62 288"">
                    <rect width=""100%"" height=""100%"" fill=""#ffffff""/>
                    <text x=""5"" y=""150"" font-family=""Arial"" font-size=""12"" fill=""#000000"">
                        {System.Net.WebUtility.HtmlEncode(text)}
                    </text>
                </svg>";

                return Convert.ToBase64String(Encoding.UTF8.GetBytes(fallbackSvg));
            }
        }

        private static bool TryParseSvgBarcodeSize(string svgDoc, out double w, out double h)
        {
            w = h = 0;

            Match vb = Regex.Match(
                svgDoc,
                @"viewBox\s*=\s*[""']\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*[""']",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            if (vb.Success)
            {
                w = double.Parse(vb.Groups[1].Value, CultureInfo.InvariantCulture);
                h = double.Parse(vb.Groups[2].Value, CultureInfo.InvariantCulture);
                return true;
            }

            Match wm = Regex.Match(
                svgDoc,
                @"\bwidth\s*=\s*[""']([\d.]+)",
                RegexOptions.IgnoreCase
            );

            Match hm = Regex.Match(
                svgDoc,
                @"\bheight\s*=\s*[""']([\d.]+)",
                RegexOptions.IgnoreCase
            );

            if (wm.Success && hm.Success)
            {
                w = double.Parse(wm.Groups[1].Value, CultureInfo.InvariantCulture);
                h = double.Parse(hm.Groups[1].Value, CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }

        private static string ExtractFirstSvgInnerXml(string doc)
        {
            int svgStart = doc.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);

            if (svgStart < 0)
                return string.Empty;

            int openEnd = doc.IndexOf('>', svgStart);

            if (openEnd < 0)
                return string.Empty;

            int innerStart = openEnd + 1;
            int innerEnd = doc.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);

            if (innerEnd < innerStart)
                return string.Empty;

            return doc.AsSpan(innerStart, innerEnd - innerStart).Trim().ToString();
        }

        [HttpGet("GetPTInvestigation")]
        public IActionResult GetPTInvestigation(
            string ids,
            int isHeaderPNG = 0,
            string printBy = null,
            string branchId = null)
        {
            try
            {
                DataTable dt = GetPatientInvestigations(
                    ids,
                    isHeaderPNG,
                    printBy,
                    branchId
                );

                var result = new List<Dictionary<string, object>>();

                foreach (DataRow row in dt.Rows)
                {
                    var dict = new Dictionary<string, object>();

                    foreach (DataColumn col in dt.Columns)
                    {
                        dict[col.ColumnName] =
                            row[col] == DBNull.Value ? null : row[col];
                    }

                    result.Add(dict);
                }

                return Ok(new
                {
                    status = true,
                    message = "Success",
                    count = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
    }
}
