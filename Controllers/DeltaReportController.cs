using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using iText.Html2pdf;
using LISD.Models;
using System.Net;
using QRCoder;
using System.Globalization;
using System.Text.RegularExpressions;
using log4net;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeltaReportController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        private static readonly ILog _log = LogManager.GetLogger(typeof(DeltaReportController));

        public DeltaReportController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        [HttpGet("download-delta-report")]
        public IActionResult DownloadDeltaReport(
            [FromQuery] string PatientInvestigationIdList,
            [FromQuery] int isHeaderPNG,
            [FromQuery] int PrintBy,
            [FromQuery] int branchId,
            [FromQuery] int? clientId = null,
            [FromQuery] bool ViewReport = false
        )
        {
            try
            {
                _log.Info($"DownloadDeltaReport API called with PatientInvestigationIdList={PatientInvestigationIdList}, isHeaderPNG={isHeaderPNG}, PrintBy={PrintBy}, branchId={branchId}, clientId={clientId}, ViewReport={ViewReport}");

                if (string.IsNullOrWhiteSpace(PatientInvestigationIdList))
                {
                    _log.Warn("PatientInvestigationIdList is required.");
                    return BadRequest(new
                    {
                        status = false,
                        message = "PatientInvestigationIdList is required."
                    });
                }

                var connectionString = _config.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _log.Error("Connection string missing.");
                    return StatusCode(500, new
                    {
                        status = false,
                        message = "Connection string missing."
                    });
                }

                string patientId = "";
                string investigationId = "";
                string reportTypeId = "";
                string doctorSignPath = "";

                string diagnosticsNo = "";
                string pName = "";
                string age = "";
                string gender = "";
                string uhid = "";
                string contactNo = "";
                string referredBy = "";
                string sampleType = "";
                string invBillingDateTime = "";
                string collectionDateTime = "";
                string reportingDateTime = "";
                string reportStatus = "";
                string referredLab = "";
                string department = "";

                string interpretation = "";
                string doctorName = "";
                string doctorTitle = "";
                string approvalDateTime = "";

                string preparedBy = "";
                string printedBy = "";

                string barCodeValue = "";
                string qrLink = "";

                string headerBodyHtml = "";
                string topBannerClass = "top-banner";
                string topBannerSpacingClass = "with-banner";

                var rowsHtml = new StringBuilder();

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    // ================= HEADER SECTION =================
                    if (isHeaderPNG == 1)
                    {
                        // If clientId is sent, execute new SP
                        if (clientId.HasValue && clientId.Value > 0)
                        {
                            using (SqlCommand cmdLetterHead = new SqlCommand("dbo.S_getLabReportLetterHeadMasterList_ByClient", con))
                            {
                                cmdLetterHead.CommandType = CommandType.StoredProcedure;
                                cmdLetterHead.Parameters.AddWithValue("@clientId", clientId.Value);

                                using (SqlDataReader r = cmdLetterHead.ExecuteReader())
                                {
                                    if (r.Read())
                                    {
                                        string letterHeadFilePath = GetString(r, "LetterHeadFilePath");

                                        if (!string.IsNullOrWhiteSpace(letterHeadFilePath))
                                        {
                                            headerBodyHtml = $@"<img alt='LETTER HEAD'
                                                src='{HtmlEncodeAttribute(letterHeadFilePath)}'
                                                style='width:100%; height:115px; display:block; object-fit:cover;' />";
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Old flow as it is
                            using (SqlCommand cmdHeader = new SqlCommand("dbo.S_GetPatientHeaderMaster", con))
                            {
                                cmdHeader.CommandType = CommandType.StoredProcedure;
                                cmdHeader.Parameters.AddWithValue("@branchId", branchId);
                                cmdHeader.Parameters.AddWithValue("@typeId", 1);

                                using (SqlDataReader headerReader = cmdHeader.ExecuteReader())
                                {
                                    if (headerReader.Read())
                                    {
                                        headerBodyHtml = GetString(headerReader, "HeaderBody");

                                        if (!string.IsNullOrWhiteSpace(headerBodyHtml))
                                        {
                                            headerBodyHtml = NormalizeHeaderHtml(headerBodyHtml);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // ✅ Keep space even when header is OFF
                        headerBodyHtml = "<div style='height:150px; width:100%;'></div>";

                        // Keep normal class (NO hide)
                        topBannerClass = "top-banner";

                        // Keep spacing same as header
                        topBannerSpacingClass = "with-banner";
                    }

                    // ================= FIRST SP =================
                    using (SqlCommand cmd1 = new SqlCommand("dbo.S_GetPatientInvestigationsForReportPrintTebular", con))
                    {
                        cmd1.CommandType = CommandType.StoredProcedure;
                        cmd1.Parameters.AddWithValue("@PatientInvestigationIdList", PatientInvestigationIdList);
                        cmd1.Parameters.AddWithValue("@isHeaderPNG", isHeaderPNG);
                        cmd1.Parameters.AddWithValue("@PrintBy", PrintBy);
                        cmd1.Parameters.AddWithValue("@branchId", branchId);

                        using (SqlDataReader r = cmd1.ExecuteReader())
                        {
                            if (!r.HasRows)
                            {
                                return NotFound(new
                                {
                                    status = false,
                                    message = "No data found from S_GetPatientInvestigationsForReportPrint."
                                });
                            }

                            if (r.Read())
                            {
                                patientId = GetString(r, "PatientId");
                                investigationId = GetString(r, "InvestigationId");
                                reportTypeId = GetString(r, "ReportTypeId");

                                doctorSignPath = GetString(r, "DoctorSignFilePath");

                                diagnosticsNo = GetString(r, "DiagnosticsNo");
                                if (string.IsNullOrWhiteSpace(diagnosticsNo))
                                    diagnosticsNo = GetString(r, "LabNo");
                                if (string.IsNullOrWhiteSpace(diagnosticsNo))
                                    diagnosticsNo = GetString(r, "PatientInvestigationId");

                                pName = GetString(r, "PName");
                                age = GetString(r, "Age");
                                gender = GetString(r, "Gender");
                                uhid = GetString(r, "UHID");
                                contactNo = GetString(r, "ContactNo");

                                referredBy = GetString(r, "InvReferdBy");
                                if (string.IsNullOrWhiteSpace(referredBy))
                                    referredBy = GetString(r, "InvPrescribedBy");

                                sampleType = GetString(r, "SampleType");
                                invBillingDateTime = GetString(r, "InvBillingDateTime");

                                collectionDateTime = GetString(r, "CollectionDateTime");
                                if (string.IsNullOrWhiteSpace(collectionDateTime))
                                    collectionDateTime = GetString(r, "InvResultDateTime");

                                reportingDateTime = GetString(r, "InvReportApprovalDateTime");
                                if (string.IsNullOrWhiteSpace(reportingDateTime))
                                    reportingDateTime = GetString(r, "InvResultDateTime");

                                reportStatus = GetString(r, "InvReportStatus");
                                if (string.IsNullOrWhiteSpace(reportStatus))
                                    reportStatus = GetString(r, "ReportStatus");

                                referredLab = GetString(r, "ReferLabName");
                                department = GetString(r, "SubSubCategoryName");

                                interpretation = GetString(r, "interpretation");

                                doctorName = GetString(r, "InvReportApprovedBy");
                                if (string.IsNullOrWhiteSpace(doctorName))
                                    doctorName = GetString(r, "ADoctor");

                                doctorTitle = GetString(r, "InvPrescribedBy");
                                approvalDateTime = GetString(r, "InvReportApprovalDateTime");

                                preparedBy = GetString(r, "InvResultEntryBy");
                                printedBy = GetString(r, "InvResultPrintBy");
                                if (string.IsNullOrWhiteSpace(printedBy))
                                    printedBy = preparedBy;

                                string labNo = GetString(r, "LabNo");
                                barCodeValue = string.IsNullOrWhiteSpace(labNo) ? "0" : labNo;

                                qrLink = GetString(r, "QRCode");
                                if (string.IsNullOrWhiteSpace(qrLink))
                                    qrLink = GetString(r, "QRCodeUrl");

                                if (string.IsNullOrWhiteSpace(qrLink))
                                {
                                    qrLink =
                                        $"http://103.217.247.236/Lab/api/DeltaReport/download-delta-report" +
                                        $"?PatientInvestigationIdList={WebUtility.UrlEncode(PatientInvestigationIdList)}" +
                                        $"&isHeaderPNG={isHeaderPNG}" +
                                        $"&PrintBy={PrintBy}" +
                                        $"&branchId={branchId}";
                                }
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(investigationId))
                    {
                        return StatusCode(500, new
                        {
                            status = false,
                            message = "PatientId or InvestigationId missing from first SP."
                        });
                    }

                    if (string.IsNullOrWhiteSpace(reportTypeId))
                        reportTypeId = "1";

                    // ================= SECOND SP =================
                    using (SqlCommand cmd2 = new SqlCommand("dbo.S_GetPatientDeltaReports", con))
                    {
                        cmd2.CommandType = CommandType.StoredProcedure;
                        cmd2.Parameters.AddWithValue("@patientId", Convert.ToInt32(patientId));
                        cmd2.Parameters.AddWithValue("@investigationId", Convert.ToInt32(investigationId));
                        cmd2.Parameters.AddWithValue("@reportTypeId", Convert.ToInt32(reportTypeId));

                        using (SqlDataReader r = cmd2.ExecuteReader())
                        {
                            if (!r.HasRows)
                            {
                                return NotFound(new
                                {
                                    status = false,
                                    message = "No data found from S_GetPatientDeltaReports."
                                });
                            }

                            while (r.Read())
                            {
                                if (string.IsNullOrWhiteSpace(department))
                                    department = GetString(r, "Department", department);

                                string testNameRaw = GetString(r, "ObservationName");
                                string resultRaw = NormalizeValue(GetString(r, "ResultValue"));
                                string unitRaw = NormalizeValue(GetString(r, "Unit"));
                                string rangeRaw = NormalizeValue(GetString(r, "DisplayRange"));
                                string methodRaw = NormalizeValue(GetString(r, "Method"));

                                string testName = HtmlEncode(testNameRaw);
                                string result = HtmlEncode(resultRaw);
                                string unit = HtmlEncode(unitRaw);
                                string range = HtmlEncode(rangeRaw);
                                string method = HtmlEncode(methodRaw);

                                string isHeader = GetString(r, "IsHeader");

                                bool isHeaderRow = isHeader == "1" ||
                                                   isHeader.Equals("true", StringComparison.OrdinalIgnoreCase);

                                if (isHeaderRow)
                                {
                                    rowsHtml.Append($@"
                                <tr>
                                    <td colspan='5' style='font-weight:bold; background:#f2f2f2;'>{testName}</td>
                                </tr>");
                                }
                                else
                                {
                                    bool isOutOfRange = IsOutOfRange(resultRaw, rangeRaw);
                                    string resultStyle = isOutOfRange ? "font-weight:bold;" : "";

                                    rowsHtml.Append($@"
                                <tr>
                                    <td>{testName}</td>
                                    <td style='{resultStyle}'>{result}</td>
                                    <td>{unit}</td>
                                    <td>{range}</td>
                                    <td>{method}</td>
                                </tr>");
                                }
                            }
                        }
                    }
                }

                string templatePath = Path.Combine(_env.ContentRootPath, "Templates", "DeltaReportTemplate.html");

                if (!System.IO.File.Exists(templatePath))
                {
                    return StatusCode(500, new
                    {
                        status = false,
                        message = "Template file not found.",
                        path = templatePath
                    });
                }

                string html = System.IO.File.ReadAllText(templatePath);

                string qrHtml = BuildQrImageHtml(qrLink);
                string barcodeHtml = BuildBarcodeImageHtml(barCodeValue);
                string doctorSignHtml = BuildDoctorSignHtml(doctorSignPath);
                string interpretationSection = BuildInterpretationSection(interpretation);
                string footerDateTime = DateTime.Now.ToString("dd-MM-yyyy hh:mm tt");

                html = html.Replace("{{TopBannerClass}}", topBannerClass);
                html = html.Replace("{{TopBannerSpacingClass}}", topBannerSpacingClass);
                html = html.Replace("{{TopBannerHtml}}", string.IsNullOrWhiteSpace(headerBodyHtml) ? "" : headerBodyHtml);

                html = html.Replace("{{DiagnosticsNo}}", HtmlEncode(diagnosticsNo));
                html = html.Replace("{{PName}}", HtmlEncode(pName));
                html = html.Replace("{{AgeGender}}", HtmlEncode($"{age} / {gender}"));
                html = html.Replace("{{ContactNo}}", HtmlEncode(contactNo));
                html = html.Replace("{{ReferredBy}}", HtmlEncode(referredBy));
                html = html.Replace("{{SampleType}}", HtmlEncode(sampleType));
                html = html.Replace("{{UHID}}", HtmlEncode(uhid));
                html = html.Replace("{{InvBillingDateTime}}", HtmlEncode(invBillingDateTime));
                html = html.Replace("{{CollectionDateTime}}", HtmlEncode(collectionDateTime));
                html = html.Replace("{{ReportingDateTime}}", HtmlEncode(reportingDateTime));
                html = html.Replace("{{ReportStatus}}", HtmlEncode(reportStatus));
                html = html.Replace("{{ReferredLab}}", HtmlEncode(referredLab));
                html = html.Replace("{{Department}}", HtmlEncode(string.IsNullOrWhiteSpace(department) ? "BIOCHEMISTRY" : department));
                html = html.Replace("{{QRCodeHtml}}", qrHtml);
                html = html.Replace("{{BarcodeHtml}}", barcodeHtml);
                html = html.Replace("{{TableRows}}", rowsHtml.ToString());
                html = html.Replace("{{InterpretationSection}}", interpretationSection);
                html = html.Replace("{{DoctorName}}", HtmlEncode(doctorName));
                html = html.Replace("{{DoctorTitle}}", HtmlEncode(doctorTitle));
                html = html.Replace("{{DoctorSign}}", doctorSignHtml);
                html = html.Replace("{{ApprovalDateTime}}", HtmlEncode(approvalDateTime));
                html = html.Replace("{{FooterDateTime}}", HtmlEncode(footerDateTime));
                html = html.Replace("{{PreparedBy}}", HtmlEncode(preparedBy));
                html = html.Replace("{{PrintedBy}}", HtmlEncode(printedBy));

                using (var stream = new MemoryStream())
                {
                    HtmlConverter.ConvertToPdf(html, stream);
                    byte[] pdfBytes = stream.ToArray();

                    string fileName = $"DeltaReport_{patientId}_{investigationId}_{reportTypeId}.pdf";

                    if (ViewReport)
                    {
                        string pdfBase64 = Convert.ToBase64String(pdfBytes);

                        return Ok(new
                        {
                            status = true,
                            message = "PDF generated successfully.",
                            fileName = fileName,
                            contentType = "application/pdf",
                            pdfBase64 = pdfBase64
                        });
                    }

                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error in DownloadDeltaReport API", ex);
                return StatusCode(500, new
                {
                    status = false,
                    message = "Error generating PDF.",
                    detail = ex.Message
                });
            }
        }

        private static string NormalizeHeaderHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";

            string updated = html;

            updated = updated.Replace("float:left;", "", StringComparison.OrdinalIgnoreCase);
            updated = updated.Replace("float: left;", "", StringComparison.OrdinalIgnoreCase);
            updated = updated.Replace("width:800px", "width:100%", StringComparison.OrdinalIgnoreCase);
            updated = updated.Replace("width: 800px", "width:100%", StringComparison.OrdinalIgnoreCase);
            updated = updated.Replace("height:130px", "height:115px", StringComparison.OrdinalIgnoreCase);
            updated = updated.Replace("height: 130px", "height:115px", StringComparison.OrdinalIgnoreCase);

            return updated;
        }

        private static string GetString(SqlDataReader r, string col, string fallback = "")
        {
            try
            {
                int i = r.GetOrdinal(col);
                if (r.IsDBNull(i)) return fallback;
                return r[col]?.ToString()?.Trim() ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string NormalizeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string HtmlEncode(string value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        private static string HtmlEncodeAttribute(string value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        private static bool IsOutOfRange(string result, string range)
        {
            if (string.IsNullOrWhiteSpace(result) || string.IsNullOrWhiteSpace(range))
                return false;

            string cleanResult = result.Trim();
            string cleanRange = range.Trim();

            if (cleanResult == "-" || cleanRange == "-")
                return false;

            cleanRange = cleanRange.Replace("–", "-").Replace("—", "-");

            var parts = cleanRange.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            if (TryParseDecimalInvariant(cleanResult, out decimal resultValue) &&
                TryParseDecimalInvariant(parts[0].Trim(), out decimal minValue) &&
                TryParseDecimalInvariant(parts[1].Trim(), out decimal maxValue))
            {
                return resultValue < minValue || resultValue > maxValue;
            }

            return false;
        }

        private static bool TryParseDecimalInvariant(string input, out decimal value)
        {
            input = (input ?? "").Trim();

            return decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out value) ||
                   decimal.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
        }

        private static string BuildQrImageHtml(string qrText)
        {
            if (string.IsNullOrWhiteSpace(qrText))
                return "";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrData);
            byte[] qrCodeBytes = pngQrCode.GetGraphic(20);

            string base64 = Convert.ToBase64String(qrCodeBytes);

            return $"<img src='data:image/png;base64,{base64}' style='width:80px; height:100px; object-fit:contain; display:block; margin:0 auto auto;' />";
        }

        private static string BuildBarcodeImageHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

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

                string rotated = FormattableString.Invariant(
                    $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{bh:0.##}"" height=""{bw:0.##}"" viewBox=""0 0 {bh:0.##} {bw:0.##}"">
<rect width=""100%"" height=""100%"" fill=""#ffffff""/>
<g transform=""translate({bh / 2:0.##},{bw / 2:0.##}) rotate(90) translate({-bw / 2:0.##},{-bh / 2:0.##})"">
{inner}
</g>
</svg>");

                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rotated));
                return $"<img src='data:image/svg+xml;base64,{base64}' style='width:40px; height:145px; object-fit:fill; display:block; margin:0 auto;' />";
            }
            catch
            {
                return $"<div style='font-size:14px; font-weight:bold; writing-mode:vertical-rl; transform:rotate(180deg); text-align:center;'>{HtmlEncode(text)}</div>";
            }
        }

        private static string BuildDoctorSignHtml(string signPath)
        {
            if (string.IsNullOrWhiteSpace(signPath))
                return "";

            try
            {
                byte[] imageBytes;

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    imageBytes = client.GetByteArrayAsync(signPath).GetAwaiter().GetResult();
                }

                if (imageBytes == null || imageBytes.Length == 0)
                    return "";

                string extension = Path.GetExtension(signPath)?.ToLowerInvariant();
                string mimeType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };

                string base64 = Convert.ToBase64String(imageBytes);

                return $"<img src='data:{mimeType};base64,{base64}' style='max-width:140px; max-height:60px; display:block; margin-top:4px; margin-left:auto;' />";
            }
            catch
            {
                return "";
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

        private static string BuildInterpretationSection(string interpretation)
        {
            if (string.IsNullOrWhiteSpace(interpretation))
                return "";

            return $@"
        <div class='interpretation-box'>
            <div class='interpretation-title'>Interpretation :</div>
            <div>{interpretation}</div>
        </div>";
        }
    }
}