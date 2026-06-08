using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using iText.Html2pdf;
using System.Text;
using ZXing;
using ZXing.Common;

namespace LISDBACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OPDRefundController : ControllerBase
    {
        private readonly IConfiguration _config;

        public OPDRefundController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("save-opd-refund")]
        public async Task<IActionResult> SaveOPDRefund([FromBody] OPDRefundRequest request)
        {
            if (request == null)
                return BadRequest(new { result = false, message = "Invalid payload" });

            if (request.OpdVisitDetails == null || request.OpdVisitDetails.Count == 0)
                return BadRequest(new { result = false, message = "OPD Visit Details required" });

            if (request.OpdRefundServices == null || request.OpdRefundServices.Count == 0)
                return BadRequest(new { result = false, message = "Refund services required" });

            if (request.PaymentDetails == null || request.PaymentDetails.Count == 0)
                return BadRequest(new { result = false, message = "Payment details required" });

            await using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await con.OpenAsync();

            await using SqlTransaction tnx = (SqlTransaction)await con.BeginTransactionAsync();

            try
            {
                var visit = request.OpdVisitDetails[0];

                decimal totalPaidAmount = request.PaymentDetails
                    .Where(x => x.PaymentModeTypeId != 4)
                    .Sum(x => x.Amount);

                int visitId = await InsertPatientVisitDetails(con, tnx, request.GlobalValues, visit, totalPaidAmount);

                int ftId = await InsertFinancialTransaction(con, tnx, request.GlobalValues, visit, visitId);

                foreach (var r in request.OpdRefundServices)
                {
                    int ftdId = await InsertFinancialTransactionDetails(
                        con,
                        tnx,
                        request.GlobalValues,
                        visit,
                        r,
                        visitId,
                        ftId
                    );

                    await ExecuteNonQuery(con, tnx, "U_CancelPatientInvestigationDetails", cmd =>
                    {
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = request.GlobalValues.UserId;
                        cmd.Parameters.Add("@FTDID", SqlDbType.Int).Value = r.FtdId;
                    });

                    await ExecuteNonQuery(con, tnx, "U_UpdateFTDRefundQTY", cmd =>
                    {
                        cmd.Parameters.Add("@FTDId", SqlDbType.Int).Value = r.FtdId;
                        cmd.Parameters.Add("@refundQty", SqlDbType.Int).Value = r.Qty;
                        cmd.Parameters.Add("@userId", SqlDbType.BigInt).Value = request.GlobalValues.UserId;
                    });
                }

                int receiptId = await InsertReceipt(con, tnx, request.GlobalValues, visit, visitId, ftId, totalPaidAmount);

                foreach (var p in request.PaymentDetails)
                {
                    if (p.PaymentModeTypeId != 4)
                    {
                        await InsertReceiptPaymentModeDetails(
                            con,
                            tnx,
                            request.GlobalValues,
                            visit,
                            receiptId,
                            p
                        );
                    }
                }

                await tnx.CommitAsync();

                return Ok(new
                {
                    result = true,
                    message = "OPD Refund Saved Successfully",
                    visitId,
                    receiptId,
                    FTID = ftId
                });
            }
            catch (Exception ex)
            {
                await tnx.RollbackAsync();

                return StatusCode(500, new
                {
                    result = false,
                    message = "Server Error found",
                    error = ex.Message
                });
            }
        }

        // generate bill for test refund

        [HttpGet("receipt-details")]
        public async Task<IActionResult> GetReceiptDetails(
        int ftId,
        int receiptId,
        int printUserId,
        bool pdf = false,
        bool isReceiptHeader = true)
        {
            await using SqlConnection con =
                new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            try
            {
                await con.OpenAsync();

                // ==========================
                // STEP 1 : Receipt Details
                // ==========================
                DataSet receiptDetails = new DataSet();

                using (SqlCommand cmd = new SqlCommand("S_GetReceiptDetails", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@FTID", ftId);
                    cmd.Parameters.AddWithValue("@isReceipt", "true");
                    cmd.Parameters.AddWithValue("@receiptId", receiptId);
                    cmd.Parameters.AddWithValue("@printUserId", printUserId);

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(receiptDetails);
                }

                // ==========================
                // STEP 2 : Previous Receipt
                // ==========================
                string receiptNo = string.Empty;
                DataTable previousReceipt = new DataTable();

                using (SqlCommand cmd = new SqlCommand("S_GetPreviousReceiptAmount", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FTID", ftId);

                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    previousReceipt.Load(reader);

                    if (previousReceipt.Rows.Count > 0)
                    {
                        if (previousReceipt.Columns.Contains("ReceiptNo"))
                            receiptNo = Convert.ToString(previousReceipt.Rows[0]["ReceiptNo"]);
                        else
                            receiptNo = Convert.ToString(previousReceipt.Rows[0][0]);
                    }
                }

                // ==========================
                // STEP 3 : Payment Details
                // ==========================
                DataTable paymentDetails = new DataTable();

                if (!string.IsNullOrWhiteSpace(receiptNo))
                {
                    using (SqlCommand cmd = new SqlCommand("S_GetReceiptPaymentDetails", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ReceiptNo", receiptNo);

                        using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                        paymentDetails.Load(reader);
                    }
                }

                // ==========================
                // STEP 4 : Generate PDF
                // ==========================
                byte[] pdfBytes = GenerateReceiptPdf(
                    receiptDetails,
                    previousReceipt,
                    paymentDetails,
                    receiptNo,
                    isReceiptHeader);

                // Download PDF
                if (pdf)
                {
                    return File(
                        pdfBytes,
                        "application/pdf",
                        $"Receipt_{receiptNo.Replace("/", "_")}.pdf");
                }

                // Return Base64 PDF
                return Ok(new
                {
                    result = true,
                    receiptNo,
                    pdfBase64 = Convert.ToBase64String(pdfBytes)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    result = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        private string BuildBarcodeDataUri(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

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

                return $"data:image/svg+xml;base64,{base64}";
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetValue(DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) &&
                   row[columnName] != DBNull.Value
                ? row[columnName]?.ToString() ?? ""
                : "";
        }

        private byte[] GenerateReceiptPdf(
            DataSet receiptDetails,
            DataTable previousReceipt,
            DataTable paymentDetails,
            string receiptNo,
            bool isReceiptHeader)
        {
            string templatePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Templates",
                "ReceiptTemplate.html");

            string html = System.IO.File.ReadAllText(templatePath);

            if (receiptDetails.Tables.Count == 0 ||
                receiptDetails.Tables[0].Rows.Count == 0)
            {
                throw new Exception("Receipt details not found.");
            }

            DataRow patient = receiptDetails.Tables[0].Rows[0];

            string receiptHeaderHtml = "";

            if (isReceiptHeader)
            {
                if (patient.Table.Columns.Contains("ReceiptHeader"))
                {
                    receiptHeaderHtml = GetValue(patient, "ReceiptHeader");
                }
                else if (patient.Table.Columns.Contains("ReceiptHeaderHtml"))
                {
                    receiptHeaderHtml = GetValue(patient, "ReceiptHeaderHtml");
                }

                if (string.IsNullOrWhiteSpace(receiptHeaderHtml))
                {
                    receiptHeaderHtml = "&nbsp;";
                }
            }
            else
            {
                receiptHeaderHtml = "<div style=\"height:120px; width:100%;\">&nbsp;</div>";
            }

            html = html.Replace("{{ReceiptHeader}}", receiptHeaderHtml);


            html = html.Replace("{{UHID}}",
                GetValue(patient, "UHID"));

            html = html.Replace("{{PatientName}}",
                GetValue(patient, "PatientName"));

            html = html.Replace("{{AgeSex}}",
                GetValue(patient, "Gender"));

            html = html.Replace("{{ContactNo}}",
                GetValue(patient, "ContactNumber"));

            html = html.Replace("{{RelativeName}}",
                GetValue(patient, "RelativeName"));

            html = html.Replace("{{PatientAddress}}",
                GetValue(patient, "Address"));

            html = html.Replace("{{Doctor}}",
                GetValue(patient, "DoctorName"));

            html = html.Replace("{{ReferDoctor}}",
                GetValue(patient, "ReferDoctorName"));

            html = html.Replace("{{Corporate}}",
                GetValue(patient, "CorporateAliasName"));

            html = html.Replace("{{LabNo}}",
                GetValue(patient, "LabNo"));

            string createdBy = GetValue(patient, "CreatedBy");
            if (string.IsNullOrWhiteSpace(createdBy))
                createdBy = "TEAM GWS";

            string printBy = GetValue(patient, "PrintBy");
            if (string.IsNullOrWhiteSpace(printBy))
                printBy = "TEAM GWS";

            string footerCenter = GetValue(patient, "FooterCenter");
            if (string.IsNullOrWhiteSpace(footerCenter))
                footerCenter = "E. & O.E.";

            string footerMessage = GetValue(patient, "FooterMessage");
            if (string.IsNullOrWhiteSpace(footerMessage))
                footerMessage = "Subject to Varanasi Jurisdiction";

            string footerRight = GetValue(patient, "FooterRight");
            if (string.IsNullOrWhiteSpace(footerRight))
                footerRight = "For GWS";

            html = html.Replace("{{ReceiptNo}}",
                receiptNo);

            html = html.Replace("{{BillNo}}",
                GetValue(patient, "BillNo"));

            html = html.Replace("{{CREATED_BY}}", createdBy);
            html = html.Replace("{{PRINT_BY}}", printBy);
            html = html.Replace("{{FOOTER_CENTER}}", footerCenter);
            html = html.Replace("{{FOOTER_MESSAGE}}", footerMessage);
            html = html.Replace("{{FOOTER_RIGHT}}", footerRight);

            // Receipt Date / Time
            string receiptDate = "";

            if (patient.Table.Columns.Contains("ReceiptDate") &&
                patient["ReceiptDate"] != DBNull.Value)
            {
                receiptDate = Convert.ToDateTime(patient["ReceiptDate"])
                    .ToString("dd-MMM-yyyy hh:mm tt");
            }
            else if (patient.Table.Columns.Contains("ReceiptDateTime") &&
                patient["ReceiptDateTime"] != DBNull.Value)
            {
                receiptDate = Convert.ToDateTime(patient["ReceiptDateTime"])
                    .ToString("dd-MMM-yyyy hh:mm tt");
            }
            else if (patient.Table.Columns.Contains("RegistrationDate") &&
                patient["RegistrationDate"] != DBNull.Value)
            {
                receiptDate = Convert.ToDateTime(patient["RegistrationDate"])
                    .ToString("dd-MMM-yyyy hh:mm tt");
            }

            html = html.Replace("{{BillDate}}", receiptDate);

            // ==========================
            // Service Rows
            // ==========================
            StringBuilder serviceRows = new StringBuilder();

            decimal totalAmount = 0;

            if (receiptDetails.Tables.Count > 0)
            {
                DataTable serviceTable = receiptDetails.Tables[0];

                foreach (DataRow row in serviceTable.Rows)
                {
                    string serviceName = GetValue(row, "ServiceName");
                    string code = GetValue(row, "Code");
                    string qty = GetValue(row, "QTY");
                    string rate = GetValue(row, "Rate");
                    string deliveryDate = GetValue(row, "DeliveryDate");

                    if (string.IsNullOrWhiteSpace(deliveryDate) && row.Table.Columns.Contains("BillDate"))
                    {
                        deliveryDate = GetValue(row, "BillDate");
                    }

                    if (decimal.TryParse(qty, out decimal qtyValue))
                    {
                        qty = qtyValue % 1 == 0
                            ? qtyValue.ToString("0")
                            : qtyValue.ToString("0.####");
                    }

                    if (decimal.TryParse(rate, out decimal rateValue))
                    {
                        rate = rateValue % 1 == 0
                            ? rateValue.ToString("0")
                            : rateValue.ToString("0.####");
                    }

                    string amount = GetValue(row, "Amount");

                    decimal amt = 0;

                    if (decimal.TryParse(amount, out decimal value))
                    {
                        amt = value;
                        totalAmount += value;
                    }

                    string netAmountText = amt % 1 == 0
                        ? amt.ToString("0")
                        : amt.ToString("0.00");

                    serviceRows.Append($@"
            <tr>
                <td>{serviceName}</td>
                <td>{code}</td>
                <td>{qty}</td>
                <td>{rate}</td>
                <td>{deliveryDate}</td>
                <td align='right'>{netAmountText}</td>
            </tr>");
                }
            }

            html = html.Replace(
                "{{ServiceRows}}",
                serviceRows.ToString());

            // ==========================
            // Payment Rows
            // ==========================
            StringBuilder paymentRows = new StringBuilder();

            foreach (DataRow row in paymentDetails.Rows)
            {
                decimal amount = 0;

                if (row["Amount"] != DBNull.Value)
                {
                    amount = Convert.ToDecimal(row["Amount"]);
                }

                string paymentMode =
                    paymentDetails.Columns.Contains("PaymentModeName")
                    ? row["PaymentModeName"]?.ToString() ?? ""
                    : "";

                paymentRows.Append($@"
                <tr>
                    <td>{receiptDate}</td>
                    <td>{receiptNo}</td>
                    <td>{amount:0.00}</td>
                    <td>{paymentMode}</td>
                    <td>gws</td>
                </tr>");
            }

            html = html.Replace(
                "{{PaymentRows}}",
                paymentRows.ToString());

            // ==========================
            // Totals
            // ==========================
            html = html.Replace(
                "{{TotalAmount}}",
                totalAmount.ToString("0.00"));

            html = html.Replace(
                "{{TotalDiscount}}",
                "0.00");

            html = html.Replace(
                "{{NetAmount}}",
                totalAmount.ToString("0.00"));

            html = html.Replace(
                "{{PaidAmount}}",
                totalAmount.ToString("0.00"));

            html = html.Replace(
                "{{BalanceAmount}}",
                "0.00");

            html = html.Replace(
                "{{AmountInWords}}",
                NumberToWords((int)totalAmount) + " Only");

            html = html.Replace(
                "{{UHIDBarcode}}",
                BuildBarcodeDataUri(GetValue(patient, "UHID")));

            string billBarcodeValue = GetValue(patient, "BillNo");
            if (string.IsNullOrWhiteSpace(billBarcodeValue))
            {
                billBarcodeValue = receiptNo;
            }

            html = html.Replace(
                "{{BillBarcode}}",
                BuildBarcodeDataUri(billBarcodeValue));

            using MemoryStream ms = new MemoryStream();

            HtmlConverter.ConvertToPdf(html, ms);

            return ms.ToArray();
        }

        private string NumberToWords(int totalAmount)
        {
            if (totalAmount == 0)
                return "Zero";

            if (totalAmount < 0)
                return "Minus " + NumberToWords(Math.Abs(totalAmount));

            var unitsMap = new[]
            {
                "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
                "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
            };

            var tensMap = new[]
            {
                "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
            };

            var words = new StringBuilder();

            if (totalAmount / 10000000 > 0)
            {
                words.Append(NumberToWords(totalAmount / 10000000) + " Crore ");
                totalAmount %= 10000000;
            }

            if (totalAmount / 100000 > 0)
            {
                words.Append(NumberToWords(totalAmount / 100000) + " Lakh ");
                totalAmount %= 100000;
            }

            if (totalAmount / 1000 > 0)
            {
                words.Append(NumberToWords(totalAmount / 1000) + " Thousand ");
                totalAmount %= 1000;
            }

            if (totalAmount / 100 > 0)
            {
                words.Append(NumberToWords(totalAmount / 100) + " Hundred ");
                totalAmount %= 100;
            }

            if (totalAmount > 0)
            {
                if (words.Length > 0)
                    words.Append("and ");

                if (totalAmount < 20)
                {
                    words.Append(unitsMap[totalAmount]);
                }
                else
                {
                    words.Append(tensMap[totalAmount / 10]);
                    if ((totalAmount % 10) > 0)
                        words.Append(" " + unitsMap[totalAmount % 10]);
                }
            }

            return words.ToString().Trim();
        }

        private static async Task<int> InsertPatientVisitDetails(
            SqlConnection con,
            SqlTransaction tnx,
            GlobalValues g,
            OpdVisitDetail v,
            decimal totalPaidAmount)
        {
            return await ExecuteScalarInt(con, tnx, "I_PatientVisitDetails", cmd =>
            {
                cmd.Parameters.Add("@hospId", SqlDbType.Int).Value = g.HospId;
                cmd.Parameters.Add("@branchId", SqlDbType.Int).Value = v.BranchId;
                cmd.Parameters.Add("@loginBranchId", SqlDbType.Int).Value = g.BranchId;
                cmd.Parameters.Add("@patientId", SqlDbType.Int).Value = v.PatientId;
                cmd.Parameters.Add("@uhid", SqlDbType.NVarChar, 50).Value = v.Uhid ?? "";
                cmd.Parameters.Add("@type", SqlDbType.NVarChar, 50).Value = "OPD";
                cmd.Parameters.Add("@typeId", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@currentAge", SqlDbType.NVarChar, 50).Value = v.CurrentAge ?? "";
                cmd.Parameters.Add("@doctorId", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@corporateId", SqlDbType.Int).Value = v.CorporateId;
                cmd.Parameters.Add("@insuranceCompanyId", SqlDbType.Int).Value = v.InsuranceCompanyId;
                cmd.Parameters.Add("@referDoctorId", SqlDbType.Int).Value = v.ReferDoctorId;

                cmd.Parameters.Add("@totalBillAmount", SqlDbType.Decimal).Value = v.GrossBillAmount;
                cmd.Parameters.Add("@totalDiscountPerOnBill", SqlDbType.Decimal).Value = v.TotalDiscPerOnBill;
                cmd.Parameters.Add("@totalDiscountAmountOnBill", SqlDbType.Decimal).Value = v.TotalDiscAmtOnBill;
                cmd.Parameters.Add("@discountApprovedById", SqlDbType.Int).Value = v.DiscAprrovedById;
                cmd.Parameters.Add("@discountReason", SqlDbType.NVarChar, 256).Value = (object?)v.DiscountReason ?? DBNull.Value;
                cmd.Parameters.Add("@roundOff", SqlDbType.Decimal).Value = v.RoundOff;
                cmd.Parameters.Add("@totalPayableAmount", SqlDbType.Decimal).Value = v.NetAmount;
                cmd.Parameters.Add("@totalPaidAmount", SqlDbType.Decimal).Value = totalPaidAmount;
                cmd.Parameters.Add("@totalBalanceAmount", SqlDbType.Decimal).Value = v.NetAmount - totalPaidAmount;
                cmd.Parameters.Add("@totalPatientPayableAmount", SqlDbType.Decimal).Value = v.NetAmount;
                cmd.Parameters.Add("@totalCorporatePayableAmount", SqlDbType.Decimal).Value = 0;
                cmd.Parameters.Add("@totalPatientPaidAmount", SqlDbType.Decimal).Value = totalPaidAmount;
                cmd.Parameters.Add("@totalCorporatePaidAmount", SqlDbType.Decimal).Value = 0;

                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = g.UserId;
                cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar, 20).Value = g.IpAddress ?? "";
                cmd.Parameters.Add("@uniqueId", SqlDbType.NVarChar, 100).Value = (object?)v.UniqueId ?? DBNull.Value;

                cmd.Parameters.Add("@CollectionDateTime", SqlDbType.DateTime).Value = new DateTime(2001, 1, 1);
            });
        }

        private static async Task<int> InsertFinancialTransaction(
            SqlConnection con,
            SqlTransaction tnx,
            GlobalValues g,
            OpdVisitDetail v,
            int visitId)
        {
            return await ExecuteScalarInt(con, tnx, "I_FinancialTransactions", cmd =>
            {
                cmd.Parameters.Add("@hospId", SqlDbType.Int).Value = g.HospId;
                cmd.Parameters.Add("@branchId", SqlDbType.Int).Value = v.BranchId;
                cmd.Parameters.Add("@loginBranchId", SqlDbType.Int).Value = g.BranchId;
                cmd.Parameters.Add("@receivingId", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@visitId", SqlDbType.Int).Value = visitId;
                cmd.Parameters.Add("@patientId", SqlDbType.Int).Value = v.PatientId;
                cmd.Parameters.Add("@tnxType", SqlDbType.NVarChar, 100).Value = "OPDRefund";
                cmd.Parameters.Add("@tnxTypeId", SqlDbType.Int).Value = 2;
                cmd.Parameters.Add("@grossAmount", SqlDbType.Decimal).Value = v.GrossBillAmount;
                cmd.Parameters.Add("@discountPercentage", SqlDbType.Decimal).Value = v.TotalDiscPerOnBill;
                cmd.Parameters.Add("@discountAmount", SqlDbType.Decimal).Value = v.TotalDiscAmtOnBill;
                cmd.Parameters.Add("@totalTaxAmount", SqlDbType.Decimal).Value = 0;
                cmd.Parameters.Add("@roundOff", SqlDbType.Decimal).Value = v.RoundOff;
                cmd.Parameters.Add("@netAmount", SqlDbType.Decimal).Value = v.NetAmount;
                cmd.Parameters.Add("@remarks", SqlDbType.NVarChar, 256).Value = (object?)v.Remarks ?? DBNull.Value;
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = g.UserId;
                cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar, 20).Value = g.IpAddress ?? "";
                cmd.Parameters.Add("@uniqueId", SqlDbType.NVarChar, 100).Value = (object?)v.UniqueId ?? DBNull.Value;
                cmd.Parameters.Add("@gstType", SqlDbType.NVarChar, 20).Value = DBNull.Value;
            });
        }

        private static async Task<int> InsertFinancialTransactionDetails(
            SqlConnection con,
            SqlTransaction tnx,
            GlobalValues g,
            OpdVisitDetail v,
            OpdRefundService r,
            int visitId,
            int ftId)
        {
            return await ExecuteScalarInt(con, tnx, "I_FinancialTransactionDetails", cmd =>
            {
                cmd.Parameters.Add("@hospId", SqlDbType.Int).Value = g.HospId;
                cmd.Parameters.Add("@branchId", SqlDbType.Int).Value = v.BranchId;
                cmd.Parameters.Add("@loginBranchId", SqlDbType.Int).Value = g.BranchId;
                cmd.Parameters.Add("@FTID", SqlDbType.Int).Value = ftId;
                cmd.Parameters.Add("@visitId", SqlDbType.Int).Value = visitId;
                cmd.Parameters.Add("@patientId", SqlDbType.Int).Value = v.PatientId;

                cmd.Parameters.Add("@serviceItemId", SqlDbType.Int).Value = r.ServiceItemId;
                cmd.Parameters.Add("@subSubCategoryId", SqlDbType.Int).Value = r.SubSubCategoryId;
                cmd.Parameters.Add("@serviceName", SqlDbType.NVarChar, 1024).Value = r.ServiceName ?? "";
                cmd.Parameters.Add("@serviceCode", SqlDbType.NVarChar, 256).Value = (object?)r.Code ?? DBNull.Value;
                cmd.Parameters.Add("@corporateAlias", SqlDbType.NVarChar, 1024).Value = (object?)r.CorporateAlias ?? DBNull.Value;
                cmd.Parameters.Add("@corporateCode", SqlDbType.NVarChar, 256).Value = (object?)r.CorporateCode ?? DBNull.Value;
                cmd.Parameters.Add("@doctorId", SqlDbType.Int).Value = (object?)r.DoctorId ?? DBNull.Value;
                cmd.Parameters.Add("@corporateId", SqlDbType.Int).Value = v.CorporateId;

                cmd.Parameters.Add("@rate", SqlDbType.Decimal).Value = r.Rate;
                cmd.Parameters.Add("@qty", SqlDbType.Decimal).Value = -1 * r.Qty;
                cmd.Parameters.Add("@grossAmt", SqlDbType.Decimal).Value = r.GrossAmt;
                cmd.Parameters.Add("@discPer", SqlDbType.Decimal).Value = r.DiscPer;
                cmd.Parameters.Add("@discAmt", SqlDbType.Decimal).Value = r.DiscAmt;
                cmd.Parameters.Add("@totalTaxPer", SqlDbType.Decimal).Value = 0;
                cmd.Parameters.Add("@totalTaxAmt", SqlDbType.Decimal).Value = 0;
                cmd.Parameters.Add("@netAmt", SqlDbType.Decimal).Value = r.NetAmt;

                cmd.Parameters.Add("@isCorporateNonPayable", SqlDbType.Int).Value = r.IsNonPayable;
                cmd.Parameters.Add("@isUnderPackage", SqlDbType.Int).Value = r.IsUnderPackage;
                cmd.Parameters.Add("@discountReason", SqlDbType.NVarChar, 256)
                    .Value = string.IsNullOrWhiteSpace(v.DiscountReason)
                        ? DBNull.Value
                        : v.DiscountReason; cmd.Parameters.Add("@rateListId", SqlDbType.Int).Value = r.RateListId;

                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = g.UserId;
                cmd.Parameters.Add("@stockId", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@EquipmentId", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar, 20).Value = g.IpAddress ?? "";
                cmd.Parameters.Add("@fromFTDID", SqlDbType.Int).Value = r.FtdId;
                cmd.Parameters.Add("@packageId", SqlDbType.Int).Value = r.PackageId;
                cmd.Parameters.Add("@billingDate", SqlDbType.Date).Value = DBNull.Value;
                cmd.Parameters.Add("@specialDiscPer", SqlDbType.Decimal).Value = 0;
                cmd.Parameters.Add("@specialDiscAmt", SqlDbType.Decimal).Value = 0;
                cmd.Parameters.Add("@deal1", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@deal2", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@ReportingBranchId", SqlDbType.Int).Value = DBNull.Value;
            });
        }

        private static async Task<int> InsertReceipt(
            SqlConnection con,
            SqlTransaction tnx,
            GlobalValues g,
            OpdVisitDetail v,
            int visitId,
            int ftId,
            decimal totalPaidAmount)
        {
            return await ExecuteScalarInt(con, tnx, "I_Receipts", cmd =>
            {
                cmd.Parameters.Add("@hospId", SqlDbType.Int).Value = g.HospId;
                cmd.Parameters.Add("@branchId", SqlDbType.Int).Value = v.BranchId;
                cmd.Parameters.Add("@loginBranchId", SqlDbType.Int).Value = g.BranchId;
                cmd.Parameters.Add("@FTID", SqlDbType.Int).Value = ftId;
                cmd.Parameters.Add("@visitId", SqlDbType.Int).Value = visitId;
                cmd.Parameters.Add("@patientId", SqlDbType.Int).Value = v.PatientId;
                cmd.Parameters.Add("@amount", SqlDbType.Decimal).Value = -1 * totalPaidAmount;
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = g.UserId;
                cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar, 20).Value = g.IpAddress ?? "";
                cmd.Parameters.Add("@uniqueId", SqlDbType.NVarChar, 100).Value = (object?)v.UniqueId ?? DBNull.Value;
                cmd.Parameters.Add("@isStore", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@isReturn", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@isBloodBank", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@isBloodBankReturn", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@isExpenseReceipt", SqlDbType.TinyInt).Value = 0;
                cmd.Parameters.Add("@isAdvanceReceipt", SqlDbType.TinyInt).Value = 0;
                cmd.Parameters.Add("@expenseId", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@isCorporateReceipt", SqlDbType.Int).Value = 0;
                cmd.Parameters.Add("@remarks", SqlDbType.NVarChar, 512).Value = DBNull.Value;
            });
        }

        private static async Task<int> InsertReceiptPaymentModeDetails(
            SqlConnection con,
            SqlTransaction tnx,
            GlobalValues g,
            OpdVisitDetail v,
            int receiptId,
            PaymentDetail p)
        {
            return await ExecuteScalarInt(con, tnx, "I_ReceiptsPaymentModeDetails", cmd =>
            {
                cmd.Parameters.Add("@hospId", SqlDbType.Int).Value = g.HospId;
                cmd.Parameters.Add("@branchId", SqlDbType.Int).Value = v.BranchId;
                cmd.Parameters.Add("@loginBranchId", SqlDbType.Int).Value = g.BranchId;
                cmd.Parameters.Add("@receiptID", SqlDbType.Int).Value = receiptId;
                cmd.Parameters.Add("@amount", SqlDbType.Decimal).Value = p.Amount;
                cmd.Parameters.Add("@paymentModeId", SqlDbType.Int).Value = p.PaymentModeId;
                cmd.Parameters.Add("@bankId", SqlDbType.Int).Value = p.BankId == 0 ? DBNull.Value : p.BankId;
                cmd.Parameters.Add("@ChequeDate", SqlDbType.Date).Value = DBNull.Value;
                cmd.Parameters.Add("@referenceNo", SqlDbType.NVarChar, 100).Value = (object?)p.RefNo ?? DBNull.Value;
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = g.UserId;
                cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar, 20).Value = g.IpAddress ?? "";
            });
        }

        private static async Task<int> ExecuteScalarInt(
            SqlConnection con,
            SqlTransaction tnx,
            string spName,
            Action<SqlCommand> addParams)
        {
            await using SqlCommand cmd = new SqlCommand(spName, con, tnx);
            cmd.CommandType = CommandType.StoredProcedure;

            addParams(cmd);

            SqlParameter result = new SqlParameter("@Result", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(result);

            object? scalar = await cmd.ExecuteScalarAsync();

            if (scalar != null && int.TryParse(scalar.ToString(), out int id))
                return id;

            return result.Value == DBNull.Value ? 0 : Convert.ToInt32(result.Value);
        }

        private static async Task ExecuteNonQuery(
            SqlConnection con,
            SqlTransaction tnx,
            string spName,
            Action<SqlCommand> addParams)
        {
            await using SqlCommand cmd = new SqlCommand(spName, con, tnx);
            cmd.CommandType = CommandType.StoredProcedure;
            addParams(cmd);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public class OPDRefundRequest
    {
        public GlobalValues GlobalValues { get; set; } = new();
        public List<OpdRefundService> OpdRefundServices { get; set; } = new();
        public List<OpdVisitDetail> OpdVisitDetails { get; set; } = new();
        public List<PaymentDetail> PaymentDetails { get; set; } = new();
    }

    public class GlobalValues
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int UserId { get; set; }
        public string? IpAddress { get; set; }
    }

    public class OpdVisitDetail
    {
        public int BranchId { get; set; }
        public int PatientId { get; set; }
        public string? Uhid { get; set; }
        public string? CurrentAge { get; set; }
        public int CorporateId { get; set; }
        public int InsuranceCompanyId { get; set; }
        public int ReferDoctorId { get; set; }
        public decimal GrossBillAmount { get; set; }
        public decimal TotalDiscPerOnBill { get; set; }
        public decimal TotalDiscAmtOnBill { get; set; }
        public int DiscAprrovedById { get; set; }
        public string? DiscountReason { get; set; }
        public decimal RoundOff { get; set; }
        public decimal NetAmount { get; set; }
        public string? Remarks { get; set; }
        public string? UniqueId { get; set; }
    }

    public class OpdRefundService
    {
        public int ServiceItemId { get; set; }
        public int SubSubCategoryId { get; set; }
        public int CategoryId { get; set; }
        public string? ServiceName { get; set; }
        public int FtdId { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal DiscPer { get; set; }
        public string? Code { get; set; }
        public string? CorporateAlias { get; set; }
        public string? CorporateCode { get; set; }
        public int? DoctorId { get; set; }
        public int IsNonPayable { get; set; }
        public int IsUnderPackage { get; set; }
        public int RateListId { get; set; }
        public int PackageId { get; set; }
    }

    public class PaymentDetail
    {
        public int PaymentModeId { get; set; }
        public int PaymentModeTypeId { get; set; }
        public decimal Amount { get; set; }
        public int BankId { get; set; }
        public string? RefNo { get; set; }
    }
}