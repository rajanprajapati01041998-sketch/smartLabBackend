using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Html2pdf;
using QRCoder;
using Path = System.IO.Path;
using iText.Layout.Element;
using iText.Layout;

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

        // ================= MAIN API =================

        [HttpGet("DownloadCombinedReport")]
        public IActionResult DownloadCombinedReport(
            int ptInvstId,
            int isHeaderPNG = 0,
            string printBy = null,
            string branchId = null)
        {
            try
            {
                if (ptInvstId <= 0)
                    return BadRequest("ptInvstId must be provided");

                // 1. Fetch Header Data
                DataTable headerData = GetPatientInvestigations(
                    ptInvstId.ToString(),
                    isHeaderPNG,
                    printBy,
                    branchId);

                if (headerData.Rows.Count == 0)
                    return NotFound("No data found");

                // Get Diagnostics Number from header data
                string diagnosticsNo = headerData.Rows[0]["LabNo"]?.ToString() ?? ptInvstId.ToString(CultureInfo.InvariantCulture);

                // 2. Fetch Results
                string resultsHtml = GetFreeText(ptInvstId);
                if (string.IsNullOrEmpty(resultsHtml))
                    resultsHtml = "<p>No results available.</p>";

                // 3. Fetch Doctor Signatures
                string doctorSignaturesHtml = GetDoctorSignatures();

                // 4. Get Current Date and Time for Footer
                string currentDateTime = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt");

                // 5. Generate QR URL
                string qrUrl = $"{Request.Scheme}://{Request.Host}/api/ReportPrint/DownloadCombinedReport?ptInvstId={ptInvstId}&isHeaderPNG={isHeaderPNG}&printBy={printBy}&branchId={branchId}";

                // 6. Generate QR + Barcode using Diagnostics Number
                string qrBase64 = GenerateQr(qrUrl);
                string barcodeBase64 = GenerateBarcode(diagnosticsNo);

                // 7. Build HTML from template and convert to PDF
                string html = BuildHtml(headerData.Rows[0], resultsHtml, qrBase64, barcodeBase64, doctorSignaturesHtml, currentDateTime);
                byte[] pdf = ConvertToPdf(html);
                return File(pdf, "application/pdf", $"Report_{diagnosticsNo}.pdf");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "";
                Console.WriteLine(ex);
                return StatusCode(500, $"Error generating report: {ex.GetType().Name}: {ex.Message}{inner}");
            }
        }

        // ================= DATABASE =================

        private DataTable GetPatientInvestigations(string ids, int isHeaderPNG, string printBy, string branchId)
        {
            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("S_GetPatientInvestigationsForReportPrint", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@PatientInvestigationIdList", ids);
            cmd.Parameters.AddWithValue("@isHeaderPNG", isHeaderPNG);
            cmd.Parameters.AddWithValue("@PrintBy", printBy ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);

            var dt = new DataTable();
            con.Open();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(dt);
            return dt;
        }

        private string GetFreeText(int id)
        {
            if (id == 0) return "<p>No results available.</p>";

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("S_GetPatientFreeTextResultsForPrint", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@PTInvstId", id);

            con.Open();
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? "<p>No results available.</p>";
        }

        // ================= DOCTOR SIGNATURES =================

        private string GetDoctorSignatures()
        {
            // Return the doctor signatures from the image
            return $@"
                <div class='doctor-card'>
                    <div class='doctor-name'>Dr. R. K. Sinha</div>
                    <div class='doctor-qualification'>MBBS, MD (Pathologist)</div>
                    <div class='doctor-designation'>Consultant Pathologist</div>
                    <div class='signature-line'></div>
                </div>
                <div class='doctor-card'>
                    <div class='doctor-name'>Dr. Uroos Fatima</div>
                    <div class='doctor-qualification'>MD, Pathologist</div>
                    <div class='doctor-designation'></div>
                    <div class='signature-line'></div>
                </div>
            ";
        }

        // ================= HTML =================

        private string BuildHtml(DataRow row, string results, string qr, string barcode, string doctorSignatures, string currentDateTime)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "ReportTemplate.html");

            if (!System.IO.File.Exists(path))
            {
                throw new FileNotFoundException($"Template file not found at: {path}");
            }

            string html = System.IO.File.ReadAllText(path);

            string Get(string c) =>
                row.Table.Columns.Contains(c) && row[c] != DBNull.Value ? row[c].ToString() : "";

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
            html = html.Replace("{{Results}}", results);
            html = html.Replace("{{DoctorSignatures}}", doctorSignatures);
            html = html.Replace("{{CurrentDateTime}}", currentDateTime);

            // Inject Images
            html = html.Replace("{{QR}}", $"data:image/png;base64,{qr}");
            html = html.Replace("{{BARCODE}}", $"data:image/svg+xml;base64,{barcode}");

            return html;
        }

        // ================= PDF (Template -> PDF) =================

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

        // ================= QR =================

        private string GenerateQr(string text)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);

            var qr = new PngByteQRCode(data);
            return Convert.ToBase64String(qr.GetGraphic(5));
        }

        // ================= BARCODE =================

        private string GenerateBarcode(string text)
        {
            try
            {
                var writer = new ZXing.BarcodeWriterSvg
                {
                    Format = ZXing.BarcodeFormat.CODE_128,
                    Options = new ZXing.Common.EncodingOptions
                    {
                        Width = 288,
                        Height = 72,
                        Margin = 2,
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

                // Horizontal barcode is bw × bh; after 90° CW it fits a narrow column of width bh.
                string rotated = FormattableString.Invariant(
                                    $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""{bh:0.##}"" height=""{bw:0.##}"" viewBox=""0 0 {bh:0.##} {bw:0.##}"">
                    <rect width=""100%"" height=""100%"" fill=""#ffffff""/>
                    <g transform=""translate({bh / 2:0.##},{bw / 2:0.##}) rotate(90) translate({-bw / 2:0.##},{-bh / 2:0.##})"">
                    {inner}
                    </g>
                    </svg>");

                return Convert.ToBase64String(Encoding.UTF8.GetBytes(rotated));
            }
            catch (Exception ex)
            {
                // Fallback: Return a simple text-based barcode if generation fails
                Console.WriteLine($"Barcode generation error: {ex.Message}");
                string fallbackSvg = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""62"" height=""288"" viewBox=""0 0 62 288"">
                    <rect width=""100%"" height=""100%"" fill=""#ffffff""/>
                    <text x=""5"" y=""150"" font-family=""Arial"" font-size=""12"" fill=""#000000"">{System.Net.WebUtility.HtmlEncode(text)}</text>
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
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (vb.Success)
            {
                w = double.Parse(vb.Groups[1].Value, CultureInfo.InvariantCulture);
                h = double.Parse(vb.Groups[2].Value, CultureInfo.InvariantCulture);
                return true;
            }

            Match wm = Regex.Match(svgDoc, @"\bwidth\s*=\s*[""']([\d.]+)", RegexOptions.IgnoreCase);
            Match hm = Regex.Match(svgDoc, @"\bheight\s*=\s*[""']([\d.]+)", RegexOptions.IgnoreCase);
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
                DataTable dt = new DataTable();

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand("S_GetPatientInvestigationsForReportPrint", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@PatientInvestigationIdList", ids);
                        cmd.Parameters.AddWithValue("@isHeaderPNG", isHeaderPNG);
                        cmd.Parameters.AddWithValue("@PrintBy", (object)printBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@branchId", (object)branchId ?? DBNull.Value);

                        con.Open();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }

                // Convert DataTable to List<Dictionary> to avoid anonymous type issues
                var result = new List<Dictionary<string, object>>();

                foreach (DataRow row in dt.Rows)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in dt.Columns)
                    {
                        dict[col.ColumnName] = row[col];
                    }
                    result.Add(dict);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
    }
}