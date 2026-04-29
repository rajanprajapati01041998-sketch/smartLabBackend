using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using iText.Html2pdf;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientController : ControllerBase
    {
        private readonly IConfiguration _config;
        private string? originalMiddleName;

        public PatientController(IConfiguration config)
        {
            _config = config;
        }

        private object DbVal(JsonElement el)
        {
            return el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined
                ? DBNull.Value
                : el.ToString() ?? (object)DBNull.Value;
        }
        // ✅ NEW: get India current datetime
        private DateTime GetIndianNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
            );
        }

        private string? GetString(JsonElement model, string key, string? defaultValue = null)
        {
            if (model.TryGetProperty(key, out JsonElement el))
            {
                if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
                    return defaultValue;

                return el.ToString();
            }
            return defaultValue;
        }

        private int GetInt(JsonElement model, string key, int defaultValue = 0)
        {
            if (model.TryGetProperty(key, out JsonElement el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int val))
                    return val;

                if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out int parsed))
                    return parsed;
            }
            return defaultValue;
        }

        private decimal GetDecimal(JsonElement model, string key, decimal defaultValue = 0)
        {
            if (model.TryGetProperty(key, out JsonElement el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out decimal val))
                    return val;

                if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out decimal parsed))
                    return parsed;
            }
            return defaultValue;
        }

        private DateTime? GetDateTime(JsonElement model, string key)
        {
            if (model.TryGetProperty(key, out JsonElement el))
            {
                if (el.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(el.GetString()) &&
                    DateTime.TryParse(el.GetString(), out DateTime dt))
                {
                    return dt;
                }
            }
            return null;
        }
        // [Authorize]
        [HttpPost("save")]
        public IActionResult SavePatient([FromBody] JsonElement model)
        {
            using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();

                using (SqlTransaction txn = con.BeginTransaction())
                {
                    try
                    {
                        int patientId = 0, visitId = 0, financialId = 0, receiptId = 0;

                        originalMiddleName = GetString(model, "MiddleName");

                        if (!model.TryGetProperty("HospId", out _))
                            return BadRequest(new { success = false, message = "HospId is required" });

                        if (!model.TryGetProperty("BranchId", out _))
                            return BadRequest(new { success = false, message = "BranchId is required" });

                        if (!model.TryGetProperty("LoginBranchId", out _))
                            return BadRequest(new { success = false, message = "LoginBranchId is required" });

                        if (!model.TryGetProperty("UserId", out _))
                            return BadRequest(new { success = false, message = "UserId is required" });

                        if (!model.TryGetProperty("Services", out JsonElement services) || services.ValueKind != JsonValueKind.Array)
                            return BadRequest(new { success = false, message = "Services array is required" });

                        if (!model.TryGetProperty("payments", out JsonElement payments) || payments.ValueKind != JsonValueKind.Array)
                            return BadRequest(new { success = false, message = "payments array is required" });

                        decimal totalService = 0;
                        decimal totalPayment = 0;

                        foreach (var s in services.EnumerateArray())
                        {
                            decimal amount = 0;
                            int qty = 1;

                            if (s.TryGetProperty("Amount", out var amountEl))
                            {
                                if (amountEl.ValueKind == JsonValueKind.Number)
                                    amount = amountEl.GetDecimal();
                                else
                                    decimal.TryParse(amountEl.ToString(), out amount);
                            }

                            if (s.TryGetProperty("qty", out var q))
                            {
                                if (q.ValueKind == JsonValueKind.Number)
                                    qty = q.GetInt32();
                                else
                                    int.TryParse(q.ToString(), out qty);
                            }

                            totalService += amount * qty;
                        }

                        foreach (var p in payments.EnumerateArray())
                        {
                            decimal amount = 0;

                            if (p.TryGetProperty("amount", out var payAmount))
                            {
                                if (payAmount.ValueKind == JsonValueKind.Number)
                                    amount = payAmount.GetDecimal();
                                else
                                    decimal.TryParse(payAmount.ToString(), out amount);
                            }

                            totalPayment += amount;
                        }

                        // =========================
                        // STEP 1: PATIENT
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("IU_PatientMaster", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                            cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                            cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                            cmd.Parameters.AddWithValue("@patientId", 0);
                            cmd.Parameters.AddWithValue("@uhid", DBNull.Value);

                            cmd.Parameters.AddWithValue("@title", (object?)GetString(model, "Title") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@firstName", (object?)GetString(model, "FirstName") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@middleName", (object?)GetString(model, "MiddleName") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@lastName", (object?)GetString(model, "LastName") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@ageYears", GetInt(model, "AgeYears"));
                            cmd.Parameters.AddWithValue("@ageMonths", GetInt(model, "AgeMonths"));
                            cmd.Parameters.AddWithValue("@ageDays", GetInt(model, "AgeDays"));

                            var dob = GetDateTime(model, "DOB");
                            cmd.Parameters.AddWithValue("@dob", dob ?? (object)DBNull.Value);

                            cmd.Parameters.AddWithValue("@gender", (object?)GetString(model, "Gender") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@maritalStatus", (object?)GetString(model, "MaritalStatus") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@relation", (object?)GetString(model, "Relation") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@relativeName", (object?)GetString(model, "RelativeName") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@aadharNumber", DBNull.Value);
                            cmd.Parameters.AddWithValue("@idProofName", DBNull.Value);
                            cmd.Parameters.AddWithValue("@idProofNumber", DBNull.Value);

                            cmd.Parameters.AddWithValue("@selfContactNumber", (object?)GetString(model, "ContactNumber") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@emergencyContactNumber", DBNull.Value);
                            cmd.Parameters.AddWithValue("@email", DBNull.Value);
                            cmd.Parameters.AddWithValue("@privilegedCardNumber", DBNull.Value);

                            cmd.Parameters.AddWithValue("@address", (object?)GetString(model, "Address") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@countryId", GetInt(model, "CountryId"));
                            cmd.Parameters.AddWithValue("@country", (object?)GetString(model, "Country") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@stateId", GetInt(model, "StateId"));
                            cmd.Parameters.AddWithValue("@state", (object?)GetString(model, "State") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@districtId", GetInt(model, "DistrictId"));
                            cmd.Parameters.AddWithValue("@district", (object?)GetString(model, "District") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@cityId", GetInt(model, "CityId"));
                            cmd.Parameters.AddWithValue("@city", (object?)GetString(model, "City") ?? DBNull.Value);

                            cmd.Parameters.AddWithValue("@insuranceCompanyId", 0);
                            cmd.Parameters.AddWithValue("@corporateId", 0);

                            cmd.Parameters.AddWithValue("@cardNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@patientImagePath", DBNull.Value);

                            cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                            cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@uniqueId", Guid.NewGuid().ToString("N"));

                            cmd.Parameters.AddWithValue("@IsVaccination", 0);
                            cmd.Parameters.AddWithValue("@vipPatient", 0);

                            cmd.Parameters.AddWithValue("@PolicyNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@PolicyCardNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@ExpiryDate", DBNull.Value);
                            cmd.Parameters.AddWithValue("@CardHolder", DBNull.Value);
                            cmd.Parameters.AddWithValue("@ReferalNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@ReferalDate", DBNull.Value);

                            SqlParameter output = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(output);
                            cmd.ExecuteNonQuery();

                            patientId = Convert.ToInt32(output.Value);

                            if (patientId == -1)
                            {
                                string invisibleChar = "\u200B";
                                string newMiddleName = (originalMiddleName ?? "") + invisibleChar;

                                cmd.Parameters["@middleName"].Value = newMiddleName;
                                cmd.ExecuteNonQuery();
                                patientId = Convert.ToInt32(output.Value);
                            }

                            if (patientId > 0)
                            {
                                using (SqlCommand createdOnCmd = new SqlCommand(@"
                            UPDATE PatientMaster
                            SET CreatedOn = @CreatedOn
                            WHERE PatientId = @PatientId", con, txn))
                                {
                                    createdOnCmd.Parameters.AddWithValue("@CreatedOn", GetIndianNow());
                                    createdOnCmd.Parameters.AddWithValue("@PatientId", patientId);
                                    createdOnCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // =========================
                        // FIX: FETCH REAL UHID FROM PatientMaster
                        // =========================
                        string uhid = "";

                        using (SqlCommand getUhidCmd = new SqlCommand(@"
                    SELECT TOP 1 UHID
                    FROM PatientMaster
                    WHERE PatientId = @PatientId", con, txn))
                        {
                            getUhidCmd.Parameters.AddWithValue("@PatientId", patientId);

                            var uhidObj = getUhidCmd.ExecuteScalar();

                            if (uhidObj == null || uhidObj == DBNull.Value || string.IsNullOrWhiteSpace(uhidObj.ToString()))
                            {
                                txn.Rollback();
                                return StatusCode(500, new
                                {
                                    success = false,
                                    message = "UHID not found in PatientMaster"
                                });
                            }

                            uhid = uhidObj.ToString()!;
                        }

                        // =========================
                        // STEP 2: VISIT
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("I_PatientVisitDetails", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                            cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                            cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            cmd.Parameters.AddWithValue("@uhid", uhid);

                            cmd.Parameters.AddWithValue("@type", "OPD");
                            cmd.Parameters.AddWithValue("@typeId", 1);
                            cmd.Parameters.AddWithValue("@currentAge", $"{GetInt(model, "AgeYears")}Y");

                            int doctorId = GetInt(model, "DoctorId", 0);
                            cmd.Parameters.AddWithValue("@doctorId", doctorId == 0 ? (object)DBNull.Value : doctorId);

                            int referDoctorId = GetInt(model, "ReferDoctorId", 0);
                            cmd.Parameters.AddWithValue("@referDoctorId", referDoctorId == 0 ? (object)DBNull.Value : referDoctorId);

                            cmd.Parameters.AddWithValue("@corporateId", GetInt(model, "CorporateId", 0));
                            cmd.Parameters.AddWithValue("@insuranceCompanyId", 0);

                            cmd.Parameters.AddWithValue("@totalBillAmount", totalService);
                            cmd.Parameters.AddWithValue("@totalPaidAmount", totalPayment);
                            cmd.Parameters.AddWithValue("@totalBalanceAmount", totalService - totalPayment);
                            cmd.Parameters.AddWithValue("@totalPayableAmount", totalService);

                            cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                            cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@uniqueId", Guid.NewGuid().ToString("N"));

                            cmd.Parameters.AddWithValue("@referLabId", GetInt(model, "ReferLabId", 0));
                            cmd.Parameters.AddWithValue("@visitTypeId", GetInt(model, "VisitTypeId", 0));
                            cmd.Parameters.AddWithValue("@fieldBoyId", GetInt(model, "FieldBoyId", 0));

                            var collectionDT = GetDateTime(model, "CollectionDateTime");
                            cmd.Parameters.AddWithValue("@CollectionDateTime", collectionDT ?? (object)DBNull.Value);

                            cmd.Parameters.AddWithValue("@statusId", DBNull.Value);
                            cmd.Parameters.AddWithValue("@status", DBNull.Value);
                            cmd.Parameters.AddWithValue("@mlc", DBNull.Value);
                            cmd.Parameters.AddWithValue("@pi", DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remark", DBNull.Value);
                            cmd.Parameters.AddWithValue("@PolicyNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@PolicyCardNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@ExpiryDate", DBNull.Value);
                            cmd.Parameters.AddWithValue("@CardHolder", DBNull.Value);
                            cmd.Parameters.AddWithValue("@ReferalNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@ReferalDate", DBNull.Value);
                            cmd.Parameters.AddWithValue("@UploadPatientDocPath", DBNull.Value);
                            cmd.Parameters.AddWithValue("@MedicalHistory", DBNull.Value);
                            cmd.Parameters.AddWithValue("@TokenNo", 0);

                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(outParam);
                            cmd.ExecuteNonQuery();

                            visitId = Convert.ToInt32(outParam.Value);

                            if (visitId > 0)
                            {
                                using (SqlCommand visitCreatedOnCmd = new SqlCommand(@"
                            UPDATE PatientVisitDetails
                            SET CreatedOn = @CreatedOn,
                                UHID = @UHID
                            WHERE VisitId = @VisitId", con, txn))
                                {
                                    visitCreatedOnCmd.Parameters.AddWithValue("@CreatedOn", GetIndianNow());
                                    visitCreatedOnCmd.Parameters.AddWithValue("@UHID", uhid);
                                    visitCreatedOnCmd.Parameters.AddWithValue("@VisitId", visitId);
                                    visitCreatedOnCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // =========================
                        // STEP 3: FINANCIAL
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("I_FinancialTransactions", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                            cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                            cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                            cmd.Parameters.AddWithValue("@visitId", visitId);
                            cmd.Parameters.AddWithValue("@patientId", patientId);

                            cmd.Parameters.AddWithValue("@tnxType", "OPD");
                            cmd.Parameters.AddWithValue("@tnxTypeId", 1);

                            cmd.Parameters.AddWithValue("@grossAmount", totalService);
                            cmd.Parameters.AddWithValue("@discountAmount", 0);
                            cmd.Parameters.AddWithValue("@netAmount", totalService);

                            cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                            cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@uniqueId", Guid.NewGuid().ToString("N"));

                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(outParam);
                            cmd.ExecuteNonQuery();

                            financialId = Convert.ToInt32(outParam.Value);
                        }

                        var serviceFtdList = new List<(int ServiceItemId, int FTDId)>();

                        foreach (var s in services.EnumerateArray())
                        {
                            using (SqlCommand cmd = new SqlCommand("I_FinancialTransactionDetails", con, txn))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;

                                int qty = 1;
                                decimal rate = 0;

                                if (s.TryGetProperty("qty", out var q))
                                {
                                    if (q.ValueKind == JsonValueKind.Number) qty = q.GetInt32();
                                    else int.TryParse(q.ToString(), out qty);
                                }

                                if (s.TryGetProperty("Amount", out var amountEl))
                                {
                                    if (amountEl.ValueKind == JsonValueKind.Number) rate = amountEl.GetDecimal();
                                    else decimal.TryParse(amountEl.ToString(), out rate);
                                }

                                decimal total = qty * rate;
                                int serviceItemId = s.TryGetProperty("ServiceItemId", out var sid) ? sid.GetInt32() : 0;
                                int subSubCategoryId = s.TryGetProperty("SubSubCategoryId", out var sub) ? sub.GetInt32() : 0;
                                string serviceName = s.TryGetProperty("ServiceName", out var sn) ? sn.GetString() ?? "" : "";

                                cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                                cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                                cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                                cmd.Parameters.AddWithValue("@FTID", financialId);
                                cmd.Parameters.AddWithValue("@visitId", visitId);
                                cmd.Parameters.AddWithValue("@patientId", patientId);
                                cmd.Parameters.AddWithValue("@corporateId", GetInt(model, "CorporateId", 0));
                                cmd.Parameters.AddWithValue("@serviceItemId", serviceItemId);
                                cmd.Parameters.AddWithValue("@subSubCategoryId", subSubCategoryId);
                                cmd.Parameters.AddWithValue("@serviceName", serviceName);

                                cmd.Parameters.AddWithValue("@rate", rate);
                                cmd.Parameters.AddWithValue("@qty", qty);
                                cmd.Parameters.AddWithValue("@grossAmt", total);
                                cmd.Parameters.AddWithValue("@netAmt", total);

                                cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                                cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);

                                SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                                {
                                    Direction = ParameterDirection.Output
                                };

                                cmd.Parameters.Add(outParam);
                                cmd.ExecuteNonQuery();

                                int generatedFtdId = Convert.ToInt32(outParam.Value);
                                serviceFtdList.Add((serviceItemId, generatedFtdId));
                            }
                        }

                        // =========================
                        // RECEIPT
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("I_Receipts", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                            cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                            cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                            cmd.Parameters.AddWithValue("@FTID", financialId);
                            cmd.Parameters.AddWithValue("@visitId", visitId);
                            cmd.Parameters.AddWithValue("@patientId", patientId);

                            cmd.Parameters.AddWithValue("@amount", totalPayment);

                            cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                            cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@uniqueId", Guid.NewGuid().ToString("N"));

                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(outParam);
                            cmd.ExecuteNonQuery();

                            receiptId = Convert.ToInt32(outParam.Value);

                            if (receiptId > 0)
                            {
                                using (SqlCommand updateReceiptCmd = new SqlCommand(@"
                            UPDATE Receipts
                            SET CreatedOn = @CreatedOn
                            WHERE ReceiptId = @ReceiptId", con, txn))
                                {
                                    updateReceiptCmd.Parameters.AddWithValue("@CreatedOn", GetIndianNow());
                                    updateReceiptCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                                    updateReceiptCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // =========================
                        // PAYMENTS
                        // =========================
                        if (receiptId > 0 && payments.ValueKind == JsonValueKind.Array && payments.GetArrayLength() > 0)
                        {
                            foreach (var p in payments.EnumerateArray())
                            {
                                int paymentModeId = 0;
                                decimal amount = 0;
                                int bankId = 0;
                                string? referenceNo = null;

                                if (p.TryGetProperty("paymentModeId", out var pm) && pm.ValueKind != JsonValueKind.Null)
                                    paymentModeId = pm.ValueKind == JsonValueKind.Number ? pm.GetInt32() : int.Parse(pm.ToString());

                                if (p.TryGetProperty("amount", out var am) && am.ValueKind != JsonValueKind.Null)
                                    amount = am.ValueKind == JsonValueKind.Number ? am.GetDecimal() : decimal.Parse(am.ToString());

                                if (p.TryGetProperty("bankId", out var bank) && bank.ValueKind != JsonValueKind.Null)
                                    bankId = bank.ValueKind == JsonValueKind.Number ? bank.GetInt32() : int.Parse(bank.ToString());

                                if (p.TryGetProperty("referenceNo", out var refNo) && refNo.ValueKind != JsonValueKind.Null)
                                    referenceNo = refNo.GetString();

                                if (paymentModeId <= 0 || amount <= 0)
                                    continue;

                                using (SqlCommand cmd = new SqlCommand("I_ReceiptsPaymentModeDetails", con, txn))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;

                                    cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                                    cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                                    cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                                    cmd.Parameters.AddWithValue("@receiptID", receiptId);
                                    cmd.Parameters.AddWithValue("@amount", amount);
                                    cmd.Parameters.AddWithValue("@paymentModeId", paymentModeId);
                                    cmd.Parameters.AddWithValue("@ChequeDate", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@bankId", bankId == 0 ? (object)DBNull.Value : bankId);
                                    cmd.Parameters.AddWithValue("@referenceNo", string.IsNullOrWhiteSpace(referenceNo) ? (object)DBNull.Value : referenceNo);
                                    cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                                    cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);

                                    SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                                    {
                                        Direction = ParameterDirection.Output
                                    };

                                    cmd.Parameters.Add(outParam);
                                    cmd.ExecuteNonQuery();

                                    int receiptPaymentModeDetailId = 0;

                                    if (outParam.Value != DBNull.Value)
                                        receiptPaymentModeDetailId = Convert.ToInt32(outParam.Value);

                                    if (receiptPaymentModeDetailId > 0)
                                    {
                                        using (SqlCommand updateDetailCmd = new SqlCommand(@"
                                    UPDATE ReceiptsPaymentModeDetails
                                    SET CreatedOn = @CreatedOn
                                    WHERE ID = @ID", con, txn))
                                        {
                                            updateDetailCmd.Parameters.AddWithValue("@CreatedOn", GetIndianNow());
                                            updateDetailCmd.Parameters.AddWithValue("@ID", receiptPaymentModeDetailId);
                                            updateDetailCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        // =========================
                        // FIX: GENERATE SAME LAB NO FOR ALL SERVICES
                        // =========================
                        int commonLabNo = 0;

                        using (SqlCommand labCmd = new SqlCommand("getLabNo", con, txn))
                        {
                            labCmd.CommandType = CommandType.StoredProcedure;
                            labCmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));

                            SqlParameter outParamLab = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            labCmd.Parameters.Add(outParamLab);
                            labCmd.ExecuteNonQuery();

                            commonLabNo = Convert.ToInt32(outParamLab.Value);
                        }

                        // =========================
                        // PatientInvestigationDetails
                        // =========================
                        // 🔹 STEP 1: Collect UNIQUE barcodes from request
                        var barcodeList = new List<string>();

                        foreach (var s in services.EnumerateArray())
                        {
                            string? bc = null;

                            if (s.TryGetProperty("Barcode", out var b1) && b1.ValueKind != JsonValueKind.Null)
                                bc = b1.GetString();
                            else if (s.TryGetProperty("barcode", out var b2) && b2.ValueKind != JsonValueKind.Null)
                                bc = b2.GetString();

                            if (!string.IsNullOrWhiteSpace(bc))
                                barcodeList.Add(bc.Trim());
                        }

                        // 🔹 DISTINCT barcodes only
                        var distinctBarcodes = barcodeList.Distinct().ToList();

                        // 🔹 STEP 2: Check barcode existence in DB (ONLY ONCE BEFORE INSERT)
                        foreach (var bc in distinctBarcodes)
                        {
                            using (SqlCommand checkCmd = new SqlCommand(@"
        SELECT COUNT(1)
        FROM PatientInvestigationDetails
        WHERE Barcode = @Barcode", con, txn))
                            {
                                checkCmd.Parameters.Add("@Barcode", SqlDbType.VarChar).Value = bc;

                                int exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                                if (exists > 0)
                                {
                                    throw new Exception($"Barcode already used: {bc}");
                                }
                            }
                        }


                        // 🔹 STEP 3: INSERT LOOP (your original logic, unchanged)

                        foreach (var s in services.EnumerateArray())
                        {
                            int investigationId = 0;

                            if (s.TryGetProperty("ServiceItemId", out var sid) && sid.ValueKind != JsonValueKind.Null)
                                investigationId = sid.GetInt32();
                            else if (s.TryGetProperty("serviceItemId", out var sid2) && sid2.ValueKind != JsonValueKind.Null)
                                investigationId = sid2.GetInt32();
                            else if (s.TryGetProperty("serviceitemid", out var sid3) && sid3.ValueKind != JsonValueKind.Null)
                                investigationId = sid3.GetInt32();

                            int matchedFtdId = serviceFtdList
                                .FirstOrDefault(x => x.ServiceItemId == investigationId)
                                .FTDId;

                            if (matchedFtdId <= 0)
                                throw new Exception($"FTDId not found for investigation/service item id {investigationId}");

                            int isUrgent = 0;

                            if (s.TryGetProperty("IsUrgent", out var urgent1) && urgent1.ValueKind != JsonValueKind.Null)
                                isUrgent = urgent1.GetInt32();
                            else if (s.TryGetProperty("isUrgent", out var urgent2) && urgent2.ValueKind != JsonValueKind.Null)
                                isUrgent = urgent2.GetInt32();

                            string? barcode = null;

                            if (s.TryGetProperty("Barcode", out var barcode1) && barcode1.ValueKind != JsonValueKind.Null)
                                barcode = barcode1.GetString();
                            else if (s.TryGetProperty("barcode", out var barcode2) && barcode2.ValueKind != JsonValueKind.Null)
                                barcode = barcode2.GetString();

                            string? testRemark = null;

                            if (s.TryGetProperty("TestRemark", out var remark1) && remark1.ValueKind != JsonValueKind.Null)
                                testRemark = remark1.GetString();
                            else if (s.TryGetProperty("testRemark", out var remark2) && remark2.ValueKind != JsonValueKind.Null)
                                testRemark = remark2.GetString();

                            int reportingBranchId = 0;

                            if (model.TryGetProperty("Investigations", out var investigations) &&
                                investigations.ValueKind == JsonValueKind.Array &&
                                investigations.GetArrayLength() > 0)
                            {
                                var inv = investigations[0];

                                if (inv.TryGetProperty("ReportingBranchId", out var rb1) && rb1.ValueKind != JsonValueKind.Null)
                                    reportingBranchId = rb1.GetInt32();
                                else if (inv.TryGetProperty("reportingBranchId", out var rb2) && rb2.ValueKind != JsonValueKind.Null)
                                    reportingBranchId = rb2.GetInt32();
                            }

                            int patientInvestigationId = 0;

                            using (SqlCommand cmd = new SqlCommand("I_PatientInvestigationDetails", con, txn))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;

                                cmd.Parameters.AddWithValue("@hospId", GetInt(model, "HospId"));
                                cmd.Parameters.AddWithValue("@branchId", GetInt(model, "BranchId"));
                                cmd.Parameters.AddWithValue("@loginBranchId", GetInt(model, "LoginBranchId"));
                                cmd.Parameters.AddWithValue("@visitId", visitId);
                                cmd.Parameters.AddWithValue("@FTDID", matchedFtdId);
                                cmd.Parameters.AddWithValue("@investigationId", investigationId);
                                cmd.Parameters.AddWithValue("@doctorId", GetInt(model, "DoctorId", 0));
                                cmd.Parameters.AddWithValue("@patientId", patientId);
                                cmd.Parameters.AddWithValue("@labNo", commonLabNo);
                                cmd.Parameters.AddWithValue("@TokenNo", 0);
                                cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                                cmd.Parameters.AddWithValue("@isUrgent", isUrgent);

                                cmd.Parameters.AddWithValue("@ReportingBranchId",
                                    reportingBranchId > 0 ? reportingBranchId : DBNull.Value);

                                cmd.Parameters.AddWithValue("@Barcode",
                                    string.IsNullOrWhiteSpace(barcode) ? DBNull.Value : barcode);

                                cmd.Parameters.AddWithValue("@testRemark",
                                    string.IsNullOrWhiteSpace(testRemark) ? DBNull.Value : testRemark);

                                cmd.Parameters.AddWithValue("@sampleTypeId", 0);
                                cmd.Parameters.AddWithValue("@LabComment", DBNull.Value);

                                cmd.Parameters.AddWithValue("@IpAddress",
                                    (object?)GetString(model, "IpAddress") ?? DBNull.Value);

                                SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                                {
                                    Direction = ParameterDirection.Output
                                };

                                cmd.Parameters.Add(outParam);
                                cmd.ExecuteNonQuery();

                                patientInvestigationId = Convert.ToInt32(outParam.Value);
                            }

                            if (patientInvestigationId > 0)
                            {
                                using (SqlCommand updateCmd = new SqlCommand(@"
                                UPDATE PatientInvestigationDetails
                                SET CreatedOn = @CreatedOn
                                WHERE PatientInvestigationId = @PatientInvestigationId", con, txn))
                                {
                                    updateCmd.Parameters.AddWithValue("@CreatedOn", GetIndianNow());
                                    updateCmd.Parameters.AddWithValue("@PatientInvestigationId", patientInvestigationId);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        txn.Commit();

                        return Ok(new
                        {
                            message = "New patient registered successfully",
                            uhid,
                            success = true,
                            patientId,
                            visitId,
                            financialId,
                            receiptId,
                            labNo = commonLabNo,
                            totalService,
                            totalPayment
                        });
                    }
                    catch (Exception ex)
                    {
                        txn.Rollback();

                        return StatusCode(500, new
                        {
                            success = false,
                            message = ex.Message,
                            details = ex.InnerException?.Message
                        });
                    }
                }
            }
        }
        // [Authorize]
        [HttpGet("get-by-uhid")]
        public async Task<IActionResult> GetPatientByUHID(string uhid, int branchId)
        {
            try
            {
                var result = new Dictionary<string, object>();

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand("S_PatientMasterByUHID", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@uhid", uhid);
                        cmd.Parameters.AddWithValue("@branchId", branchId);

                        await con.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    result[reader.GetName(i)] = reader.IsDBNull(i)
                                        ? null!
                                        : reader.GetValue(i);
                                }
                            }
                        }
                    }
                }

                if (result.Count == 0)
                    return NotFound(new { success = false, message = "Patient not found" });

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpGet("get-patient-investigation")]
        public async Task<IActionResult> GetPatientInvestigation(
            string? branchId = null,
            string typeId = "0",
            string? uhid = null,
            string? ipdNo = null,
            string? labNo = null,
            string? fromDate = null,
            string? toDate = null,
            string? barCode = null,
            string? subCategoryId = null,
            string? corporateId = null,
            string? branchIdList = null,
            string? subSubCategoryId = null,
            string? investigationName = null,
            string? patientName = null,
            string roleId = "0",
            string? filter = null
        )
        {
            try
            {
                var list = new List<Dictionary<string, object?>>();

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("S_GetPatientInvestigationDetail", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@branchId", string.IsNullOrWhiteSpace(branchId) ? (object)DBNull.Value : branchId);
                        cmd.Parameters.AddWithValue("@typeId", string.IsNullOrWhiteSpace(typeId) ? "0" : typeId);
                        cmd.Parameters.AddWithValue("@uhid", string.IsNullOrWhiteSpace(uhid) ? (object)DBNull.Value : uhid);
                        cmd.Parameters.AddWithValue("@ipdNo", string.IsNullOrWhiteSpace(ipdNo) ? (object)DBNull.Value : ipdNo);
                        cmd.Parameters.AddWithValue("@labNo", string.IsNullOrWhiteSpace(labNo) ? (object)DBNull.Value : labNo);
                        cmd.Parameters.AddWithValue("@fromDate", string.IsNullOrWhiteSpace(fromDate) ? (object)DBNull.Value : fromDate);
                        cmd.Parameters.AddWithValue("@toDate", string.IsNullOrWhiteSpace(toDate) ? (object)DBNull.Value : toDate);
                        cmd.Parameters.AddWithValue("@barCode", string.IsNullOrWhiteSpace(barCode) ? (object)DBNull.Value : barCode);
                        cmd.Parameters.AddWithValue("@subCategoryId", string.IsNullOrWhiteSpace(subCategoryId) || subCategoryId == "0" ? (object)DBNull.Value : subCategoryId);
                        cmd.Parameters.AddWithValue("@corporateId", string.IsNullOrWhiteSpace(corporateId) ? (object)DBNull.Value : corporateId);
                        cmd.Parameters.AddWithValue("@branchIdList", string.IsNullOrWhiteSpace(branchIdList) ? (object)DBNull.Value : branchIdList);
                        cmd.Parameters.AddWithValue("@subSubCategoryId", string.IsNullOrWhiteSpace(subSubCategoryId) || subSubCategoryId == "0" ? (object)DBNull.Value : subSubCategoryId);
                        cmd.Parameters.AddWithValue("@investigationName", string.IsNullOrWhiteSpace(investigationName) ? (object)DBNull.Value : investigationName);
                        cmd.Parameters.AddWithValue("@patientName", string.IsNullOrWhiteSpace(patientName) ? (object)DBNull.Value : patientName);
                        cmd.Parameters.AddWithValue("@roleId", string.IsNullOrWhiteSpace(roleId) ? "0" : roleId);
                        cmd.Parameters.AddWithValue("@filter", string.IsNullOrWhiteSpace(filter) ? (object)DBNull.Value : filter);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object?>();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string columnName = reader.GetName(i);
                                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    row[columnName] = value;
                                }

                                list.Add(row);
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "Data fetched successfully",
                    count = list.Count,
                    data = list
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }


        // update patient info


        [HttpPost("update-patient")]
        public IActionResult UpdatePatient([FromBody] UpdatePatientRequest request)
        {
            using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            con.Open();

            using SqlTransaction txn = con.BeginTransaction();

            try
            {
                if (request == null || request.Patient == null)
                    return BadRequest(new { success = false, message = "Patient data is required" });

                var patient = request.Patient;

                // only these 4 required
                if (!patient.HospId.HasValue || patient.HospId.Value <= 0)
                    return BadRequest(new { success = false, message = "HospId is required" });

                if (!patient.BranchId.HasValue || patient.BranchId.Value <= 0)
                    return BadRequest(new { success = false, message = "BranchId is required" });

                if (!patient.LoginBranchId.HasValue || patient.LoginBranchId.Value <= 0)
                    return BadRequest(new { success = false, message = "LoginBranchId is required" });

                if (string.IsNullOrWhiteSpace(patient.UHID))
                    return BadRequest(new { success = false, message = "UHID is required" });

                using SqlCommand cmd = new SqlCommand("IU_PatientMaster", con, txn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@hospId", patient.HospId.Value);
                cmd.Parameters.AddWithValue("@branchId", patient.BranchId.Value);
                cmd.Parameters.AddWithValue("@loginBranchId", patient.LoginBranchId.Value);
                cmd.Parameters.AddWithValue("@patientId", patient.PatientId ?? 0);
                cmd.Parameters.AddWithValue("@uhid", patient.UHID);

                cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(patient.Title) ? (object)DBNull.Value : patient.Title);
                cmd.Parameters.AddWithValue("@firstName", string.IsNullOrWhiteSpace(patient.FirstName) ? (object)DBNull.Value : patient.FirstName);
                cmd.Parameters.AddWithValue("@middleName", string.IsNullOrWhiteSpace(patient.MiddleName) ? (object)DBNull.Value : patient.MiddleName);
                cmd.Parameters.AddWithValue("@lastName", string.IsNullOrWhiteSpace(patient.LastName) ? (object)DBNull.Value : patient.LastName);

                cmd.Parameters.AddWithValue("@ageYears", patient.AgeYears ?? 0);
                cmd.Parameters.AddWithValue("@ageMonths", patient.AgeMonths ?? 0);
                cmd.Parameters.AddWithValue("@ageDays", patient.AgeDays ?? 0);
                cmd.Parameters.AddWithValue("@dob", patient.DOB ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@gender", string.IsNullOrWhiteSpace(patient.Gender) ? (object)DBNull.Value : patient.Gender);
                cmd.Parameters.AddWithValue("@maritalStatus", string.IsNullOrWhiteSpace(patient.MaritalStatus) ? (object)DBNull.Value : patient.MaritalStatus);
                cmd.Parameters.AddWithValue("@relation", string.IsNullOrWhiteSpace(patient.Relation) ? (object)DBNull.Value : patient.Relation);
                cmd.Parameters.AddWithValue("@relativeName", string.IsNullOrWhiteSpace(patient.RelativeName) ? (object)DBNull.Value : patient.RelativeName);

                cmd.Parameters.AddWithValue("@aadharNumber", string.IsNullOrWhiteSpace(patient.AadharNumber) ? (object)DBNull.Value : patient.AadharNumber);
                cmd.Parameters.AddWithValue("@idProofName", string.IsNullOrWhiteSpace(patient.IdProofName) ? (object)DBNull.Value : patient.IdProofName);
                cmd.Parameters.AddWithValue("@idProofNumber", string.IsNullOrWhiteSpace(patient.IdProofNumber) ? (object)DBNull.Value : patient.IdProofNumber);

                cmd.Parameters.AddWithValue("@selfContactNumber", string.IsNullOrWhiteSpace(patient.ContactNumber) ? (object)DBNull.Value : patient.ContactNumber);
                cmd.Parameters.AddWithValue("@emergencyContactNumber", string.IsNullOrWhiteSpace(patient.EmergencyContactNumber) ? (object)DBNull.Value : patient.EmergencyContactNumber);
                cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(patient.Email) ? (object)DBNull.Value : patient.Email);

                cmd.Parameters.AddWithValue("@privilegedCardNumber", string.IsNullOrWhiteSpace(patient.PrivilegedCardNumber) ? (object)DBNull.Value : patient.PrivilegedCardNumber);
                cmd.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(patient.Address) ? (object)DBNull.Value : patient.Address);

                cmd.Parameters.AddWithValue("@countryId", patient.CountryId ?? 0);
                cmd.Parameters.AddWithValue("@country", string.IsNullOrWhiteSpace(patient.Country) ? (object)DBNull.Value : patient.Country);
                cmd.Parameters.AddWithValue("@stateId", patient.StateId ?? 0);
                cmd.Parameters.AddWithValue("@state", string.IsNullOrWhiteSpace(patient.State) ? (object)DBNull.Value : patient.State);
                cmd.Parameters.AddWithValue("@districtId", patient.DistrictId ?? 0);
                cmd.Parameters.AddWithValue("@district", string.IsNullOrWhiteSpace(patient.District) ? (object)DBNull.Value : patient.District);
                cmd.Parameters.AddWithValue("@cityId", patient.CityId ?? 0);
                cmd.Parameters.AddWithValue("@city", string.IsNullOrWhiteSpace(patient.City) ? (object)DBNull.Value : patient.City);

                cmd.Parameters.AddWithValue("@insuranceCompanyId", patient.InsuranceCompanyId ?? 0);
                cmd.Parameters.AddWithValue("@corporateId", patient.CorporateId ?? 0);
                cmd.Parameters.AddWithValue("@cardNo", string.IsNullOrWhiteSpace(patient.CardNo) ? (object)DBNull.Value : patient.CardNo);

                cmd.Parameters.AddWithValue("@patientImagePath", string.IsNullOrWhiteSpace(request.PatientImagePath) ? (object)DBNull.Value : request.PatientImagePath);

                cmd.Parameters.AddWithValue("@userId", patient.UserId ?? 0);
                cmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(patient.IpAddress) ? (object)DBNull.Value : patient.IpAddress);
                cmd.Parameters.AddWithValue("@uniqueId", string.IsNullOrWhiteSpace(patient.UniqueId) ? (object)DBNull.Value : patient.UniqueId);

                cmd.Parameters.AddWithValue("@IsVaccination", patient.IsVaccination ?? 0);
                // cmd.Parameters.AddWithValue("@vipPatient", patient.VIPPatient ?? 0);

                cmd.Parameters.AddWithValue("@PolicyNo", string.IsNullOrWhiteSpace(patient.PolicyNo) ? (object)DBNull.Value : patient.PolicyNo);
                cmd.Parameters.AddWithValue("@PolicyCardNo", string.IsNullOrWhiteSpace(patient.PolicyCardNo) ? (object)DBNull.Value : patient.PolicyCardNo);
                cmd.Parameters.AddWithValue("@ExpiryDate", string.IsNullOrWhiteSpace(patient.ExpiryDate) ? (object)DBNull.Value : patient.ExpiryDate);
                cmd.Parameters.AddWithValue("@CardHolder", string.IsNullOrWhiteSpace(patient.CardHolder) ? (object)DBNull.Value : patient.CardHolder);
                cmd.Parameters.AddWithValue("@ReferalNo", string.IsNullOrWhiteSpace(patient.ReferalNo) ? (object)DBNull.Value : patient.ReferalNo);
                cmd.Parameters.AddWithValue("@ReferalDate", string.IsNullOrWhiteSpace(patient.ReferalDate) ? (object)DBNull.Value : patient.ReferalDate);

                SqlParameter output = new SqlParameter("@Result", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(output);

                cmd.ExecuteNonQuery();

                int result = Convert.ToInt32(output.Value);

                if (result == -1)
                {
                    txn.Rollback();
                    return BadRequest(new { success = false, message = "Patient Already Exists" });
                }

                if (result == -2)
                {
                    txn.Rollback();
                    return BadRequest(new { success = false, message = "Aadhar Already Exists" });
                }

                txn.Commit();

                return Ok(new
                {
                    success = true,
                    message = (patient.PatientId ?? 0) == 0 ? "Patient Saved Successfully" : "Patient Updated Successfully",
                    patientId = result
                });
            }
            catch (Exception ex)
            {
                txn.Rollback();

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpGet("get-patient-bill-details")]
        public async Task<IActionResult> GetPatientBillDetails(int visitId)
        {
            try
            {
                if (visitId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "VisitId is required"
                    });
                }

                var list = new List<Dictionary<string, object?>>();

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("S_getPatientBillDetailsByVisitId", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@visitId", visitId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object?>();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string columnName = reader.GetName(i);
                                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    row[columnName] = value;
                                }

                                list.Add(row);
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    count = list.Count,
                    data = list
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }


        // update service item

        [HttpPost("update-patient-services")]
        public async Task<IActionResult> UpdatePatientServices([FromBody] UpdatePatientServicesRequest request)
        {
            using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await con.OpenAsync();

            using SqlTransaction txn = con.BeginTransaction();

            try
            {
                if (request == null)
                    return BadRequest(new { success = false, message = "Request body is required" });

                if ((request.PatientId ?? 0) <= 0 && string.IsNullOrWhiteSpace(request.UHID))
                    return BadRequest(new { success = false, message = "PatientId or UHID is required" });

                if (request.BranchId <= 0)
                    return BadRequest(new { success = false, message = "BranchId is required" });

                if (request.LoginBranchId <= 0)
                    return BadRequest(new { success = false, message = "LoginBranchId is required" });

                if (request.UserId <= 0)
                    return BadRequest(new { success = false, message = "UserId is required" });

                if (request.Services == null || request.Services.Count == 0)
                    return BadRequest(new { success = false, message = "Services are required" });

                int patientId = request.PatientId ?? 0;
                string? uhid = request.UHID;
                int ftId = 0;
                int visitId = 0;
                int doctorId = 0;
                int commonLabNo = 0;
                decimal totalPaidAmount = 0;

                if (patientId <= 0)
                {
                    using SqlCommand patientCmd = new SqlCommand(@"
                SELECT TOP 1 PatientId
                FROM PatientMaster
                WHERE UHID = @UHID", con, txn);

                    patientCmd.Parameters.AddWithValue("@UHID", uhid!);

                    var patientObj = await patientCmd.ExecuteScalarAsync();

                    if (patientObj == null || patientObj == DBNull.Value)
                    {
                        txn.Rollback();
                        return NotFound(new { success = false, message = "Patient not found" });
                    }

                    patientId = Convert.ToInt32(patientObj);
                }
                else
                {
                    using SqlCommand uhidCmd = new SqlCommand(@"
                SELECT TOP 1 UHID
                FROM PatientMaster
                WHERE PatientId = @PatientId", con, txn);

                    uhidCmd.Parameters.AddWithValue("@PatientId", patientId);

                    var uhidObj = await uhidCmd.ExecuteScalarAsync();

                    if (uhidObj != null && uhidObj != DBNull.Value)
                        uhid = Convert.ToString(uhidObj);
                }

                using (SqlCommand ftCmd = new SqlCommand(@"
                SELECT TOP 1
                    ft.FTId,
                    ft.VisitId,
                    ISNULL(pvd.TotalPaidAmount, 0) AS TotalPaidAmount,
                    ISNULL(pvd.DoctorId, 0) AS DoctorId
                FROM FinancialTransactions ft
                INNER JOIN PatientVisitDetails pvd ON pvd.VisitId = ft.VisitId
                WHERE ft.PatientId = @PatientId
                AND ft.BranchId = @BranchId
                AND ISNULL(ft.IsCancel, 0) = 0
                AND ISNULL(pvd.IsCancel, 0) = 0
                ORDER BY ft.FTId DESC", con, txn))
                {
                    ftCmd.Parameters.AddWithValue("@PatientId", patientId);
                    ftCmd.Parameters.AddWithValue("@BranchId", request.BranchId);

                    using SqlDataReader reader = await ftCmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        txn.Rollback();
                        return NotFound(new { success = false, message = "Financial transaction not found" });
                    }

                    ftId = reader["FTId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FTId"]);
                    visitId = reader["VisitId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["VisitId"]);
                    totalPaidAmount = reader["TotalPaidAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalPaidAmount"]);
                    doctorId = reader["DoctorId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DoctorId"]);
                }

                if (ftId <= 0 || visitId <= 0)
                {
                    txn.Rollback();
                    return BadRequest(new { success = false, message = "Valid FTId or VisitId not found" });
                }

                using (SqlCommand labFindCmd = new SqlCommand(@"
                SELECT TOP 1 LabNo
                FROM PatientInvestigationDetails
                WHERE VisitId = @VisitId
                AND PatientId = @PatientId
                AND ISNULL(IsCancel, 0) = 0
                AND ISNULL(LabNo, 0) > 0
                ORDER BY PatientInvestigationId ASC", con, txn))
                {
                    labFindCmd.Parameters.AddWithValue("@VisitId", visitId);
                    labFindCmd.Parameters.AddWithValue("@PatientId", patientId);

                    var labObj = await labFindCmd.ExecuteScalarAsync();

                    if (labObj != null && labObj != DBNull.Value)
                        commonLabNo = Convert.ToInt32(labObj);
                }

                if (commonLabNo <= 0)
                {
                    using SqlCommand labCmd = new SqlCommand("getLabNo", con, txn);
                    labCmd.CommandType = CommandType.StoredProcedure;
                    labCmd.Parameters.AddWithValue("@branchId", request.BranchId);

                    SqlParameter outParamLab = new SqlParameter("@Result", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };

                    labCmd.Parameters.Add(outParamLab);
                    await labCmd.ExecuteNonQueryAsync();

                    commonLabNo = Convert.ToInt32(outParamLab.Value);
                }

                if (commonLabNo <= 0)
                {
                    txn.Rollback();
                    return BadRequest(new { success = false, message = "Unable to generate LabNo" });
                }

                foreach (var service in request.Services)
                {
                    if (service.ServiceItemId <= 0)
                    {
                        txn.Rollback();
                        return BadRequest(new { success = false, message = "ServiceItemId is required" });
                    }

                    int qty = service.Qty <= 0 ? 1 : service.Qty;
                    decimal rate = service.Amount < 0 ? 0 : service.Amount;
                    decimal total = qty * rate;

                    int ftdId = 0;

                    using (SqlCommand findCmd = new SqlCommand(@"
                    SELECT TOP 1 FTDId
                    FROM FinancialTransactionDetails
                    WHERE FTId = @FTId
                    AND PatientId = @PatientId
                    AND VisitId = @VisitId
                    AND ServiceItemId = @ServiceItemId
                    AND ISNULL(IsCancel, 0) = 0
                    ORDER BY FTDId DESC", con, txn))
                    {
                        findCmd.Parameters.AddWithValue("@FTId", ftId);
                        findCmd.Parameters.AddWithValue("@PatientId", patientId);
                        findCmd.Parameters.AddWithValue("@VisitId", visitId);
                        findCmd.Parameters.AddWithValue("@ServiceItemId", service.ServiceItemId);

                        var ftdObj = await findCmd.ExecuteScalarAsync();

                        if (ftdObj != null && ftdObj != DBNull.Value)
                            ftdId = Convert.ToInt32(ftdObj);
                    }

                    if (ftdId > 0)
                    {
                        using SqlCommand cmd = new SqlCommand("U_FinancialTransactionDetails", con, txn);
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@FTDId", ftdId);
                        cmd.Parameters.AddWithValue("@serviceItemId", service.ServiceItemId);
                        cmd.Parameters.AddWithValue("@subSubCategoryId", service.SubSubCategoryId);
                        cmd.Parameters.AddWithValue("@serviceName", string.IsNullOrWhiteSpace(service.ServiceName) ? (object)DBNull.Value : service.ServiceName);
                        cmd.Parameters.AddWithValue("@serviceCode", DBNull.Value);
                        cmd.Parameters.AddWithValue("@corporateAlias", DBNull.Value);
                        cmd.Parameters.AddWithValue("@corporateCode", DBNull.Value);
                        cmd.Parameters.AddWithValue("@doctorId", doctorId == 0 ? (object)DBNull.Value : doctorId);
                        cmd.Parameters.AddWithValue("@corporateId", service.CorporateId);
                        cmd.Parameters.AddWithValue("@rate", rate);
                        cmd.Parameters.AddWithValue("@qty", qty);
                        cmd.Parameters.AddWithValue("@grossAmt", total);
                        cmd.Parameters.AddWithValue("@discPer", 0);
                        cmd.Parameters.AddWithValue("@discAmt", 0);
                        cmd.Parameters.AddWithValue("@totalTaxPer", 0);
                        cmd.Parameters.AddWithValue("@totalTaxAmt", 0);
                        cmd.Parameters.AddWithValue("@netAmt", total);
                        cmd.Parameters.AddWithValue("@isCorporateNonPayable", 0);
                        cmd.Parameters.AddWithValue("@isUnderPackage", 0);
                        cmd.Parameters.AddWithValue("@discountReason", DBNull.Value);
                        cmd.Parameters.AddWithValue("@rateListId", 0);
                        cmd.Parameters.AddWithValue("@userId", request.UserId);
                        cmd.Parameters.AddWithValue("@stockId", DBNull.Value);
                        cmd.Parameters.AddWithValue("@EquipmentId", DBNull.Value);
                        cmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress);
                        cmd.Parameters.AddWithValue("@fromFTDID", 0);
                        cmd.Parameters.AddWithValue("@packageId", 0);
                        cmd.Parameters.AddWithValue("@billingDate", DBNull.Value);
                        cmd.Parameters.AddWithValue("@deal1", 0);
                        cmd.Parameters.AddWithValue("@deal2", 0);
                        cmd.Parameters.AddWithValue("@s_rate", 0);
                        cmd.Parameters.AddWithValue("@s_qty", 0);
                        cmd.Parameters.AddWithValue("@s_grossAmt", 0);
                        cmd.Parameters.AddWithValue("@s_discPer", 0);
                        cmd.Parameters.AddWithValue("@s_discAmt", 0);
                        cmd.Parameters.AddWithValue("@s_totalTaxPer", 0);
                        cmd.Parameters.AddWithValue("@s_totalTaxAmt", 0);
                        cmd.Parameters.AddWithValue("@s_netAmt", 0);
                        cmd.Parameters.AddWithValue("@isCancel", 0);
                        cmd.Parameters.AddWithValue("@specialDiscPer", 0);
                        cmd.Parameters.AddWithValue("@specialDiscAmt", 0);

                        SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };

                        cmd.Parameters.Add(outParam);
                        await cmd.ExecuteNonQueryAsync();

                        using (SqlCommand updPidCmd = new SqlCommand(@"
                        UPDATE PatientInvestigationDetails
                        SET
                            IsUrgent = @IsUrgent,
                            Barcode = @Barcode,
                            TestRemark = @TestRemark,
                            LastModifiedBy = @UserId,
                            LastModifiedOn = GETDATE(),
                            IpAddress = @IpAddress
                        WHERE VisitId = @VisitId
                        AND PatientId = @PatientId
                        AND FTDId = @FTDId
                        AND InvestigationId = @InvestigationId
                        AND ISNULL(IsCancel, 0) = 0", con, txn))
                        {
                            updPidCmd.Parameters.AddWithValue("@IsUrgent", service.IsUrgent);
                            updPidCmd.Parameters.AddWithValue("@Barcode", string.IsNullOrWhiteSpace(service.Barcode) ? (object)DBNull.Value : service.Barcode);
                            updPidCmd.Parameters.AddWithValue("@TestRemark", string.IsNullOrWhiteSpace(service.TestRemark) ? (object)DBNull.Value : service.TestRemark);
                            updPidCmd.Parameters.AddWithValue("@UserId", request.UserId);
                            updPidCmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress);
                            updPidCmd.Parameters.AddWithValue("@VisitId", visitId);
                            updPidCmd.Parameters.AddWithValue("@PatientId", patientId);
                            updPidCmd.Parameters.AddWithValue("@FTDId", ftdId);
                            updPidCmd.Parameters.AddWithValue("@InvestigationId", service.ServiceItemId);

                            await updPidCmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        using (SqlCommand cmd = new SqlCommand("I_FinancialTransactionDetails", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", 1);
                            cmd.Parameters.AddWithValue("@branchId", request.BranchId);
                            cmd.Parameters.AddWithValue("@loginBranchId", request.LoginBranchId);
                            cmd.Parameters.AddWithValue("@FTID", ftId);
                            cmd.Parameters.AddWithValue("@visitId", visitId);
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            cmd.Parameters.AddWithValue("@corporateId", service.CorporateId);
                            cmd.Parameters.AddWithValue("@serviceItemId", service.ServiceItemId);
                            cmd.Parameters.AddWithValue("@subSubCategoryId", service.SubSubCategoryId);
                            cmd.Parameters.AddWithValue("@serviceName", string.IsNullOrWhiteSpace(service.ServiceName) ? (object)DBNull.Value : service.ServiceName);
                            cmd.Parameters.AddWithValue("@rate", rate);
                            cmd.Parameters.AddWithValue("@qty", qty);
                            cmd.Parameters.AddWithValue("@grossAmt", total);
                            cmd.Parameters.AddWithValue("@netAmt", total);
                            cmd.Parameters.AddWithValue("@userId", request.UserId);
                            cmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress);

                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(outParam);
                            await cmd.ExecuteNonQueryAsync();

                            ftdId = Convert.ToInt32(outParam.Value);
                        }

                        if (ftdId <= 0)
                        {
                            txn.Rollback();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Unable to insert FinancialTransactionDetails for ServiceItemId {service.ServiceItemId}"
                            });
                        }

                        using (SqlCommand cmd = new SqlCommand("I_PatientInvestigationDetails", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", 1);
                            cmd.Parameters.AddWithValue("@branchId", request.BranchId);
                            cmd.Parameters.AddWithValue("@loginBranchId", request.LoginBranchId);
                            cmd.Parameters.AddWithValue("@visitId", visitId);
                            cmd.Parameters.AddWithValue("@FTDID", ftdId);
                            cmd.Parameters.AddWithValue("@investigationId", service.ServiceItemId);
                            cmd.Parameters.AddWithValue("@doctorId", doctorId);
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            cmd.Parameters.AddWithValue("@labNo", commonLabNo);
                            cmd.Parameters.AddWithValue("@TokenNo", 0);
                            cmd.Parameters.AddWithValue("@userId", request.UserId);
                            cmd.Parameters.AddWithValue("@isUrgent", service.IsUrgent);
                            cmd.Parameters.AddWithValue("@ReportingBranchId", DBNull.Value);
                            cmd.Parameters.AddWithValue("@Barcode", string.IsNullOrWhiteSpace(service.Barcode) ? (object)DBNull.Value : service.Barcode);
                            cmd.Parameters.AddWithValue("@testRemark", string.IsNullOrWhiteSpace(service.TestRemark) ? (object)DBNull.Value : service.TestRemark);
                            cmd.Parameters.AddWithValue("@sampleTypeId", 0);
                            cmd.Parameters.AddWithValue("@LabComment", DBNull.Value);
                            cmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress);

                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(outParam);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (SqlCommand updInsertedPidCmd = new SqlCommand(@"
                        UPDATE PatientInvestigationDetails
                        SET
                            LastModifiedBy = @UserId,
                            LastModifiedOn = GETDATE(),
                            IpAddress = @IpAddress
                        WHERE VisitId = @VisitId
                        AND PatientId = @PatientId
                        AND FTDId = @FTDId
                        AND InvestigationId = @InvestigationId
                        AND ISNULL(IsCancel, 0) = 0", con, txn))
                        {
                            updInsertedPidCmd.Parameters.AddWithValue("@UserId", request.UserId);
                            updInsertedPidCmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress);
                            updInsertedPidCmd.Parameters.AddWithValue("@VisitId", visitId);
                            updInsertedPidCmd.Parameters.AddWithValue("@PatientId", patientId);
                            updInsertedPidCmd.Parameters.AddWithValue("@FTDId", ftdId);
                            updInsertedPidCmd.Parameters.AddWithValue("@InvestigationId", service.ServiceItemId);

                            await updInsertedPidCmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                decimal grossAmount = 0;
                decimal netAmount = 0;

                using (SqlCommand totalCmd = new SqlCommand(@"
                SELECT
                    ISNULL(SUM(ISNULL(GrossAmt, 0)), 0) AS GrossAmount,
                    ISNULL(SUM(ISNULL(NetAmt, 0)), 0) AS NetAmount
                FROM FinancialTransactionDetails
                WHERE FTId = @FTId
                AND PatientId = @PatientId
                AND VisitId = @VisitId
                AND ISNULL(IsCancel, 0) = 0", con, txn))
                {
                    totalCmd.Parameters.AddWithValue("@FTId", ftId);
                    totalCmd.Parameters.AddWithValue("@PatientId", patientId);
                    totalCmd.Parameters.AddWithValue("@VisitId", visitId);

                    using SqlDataReader reader = await totalCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        grossAmount = reader["GrossAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["GrossAmount"]);
                        netAmount = reader["NetAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["NetAmount"]);
                    }
                }

                decimal finalNetAmount = netAmount - request.DiscountAmount;
                if (finalNetAmount < 0) finalNetAmount = 0;

                using (SqlCommand cmd = new SqlCommand("U_FinancialTransactions", con, txn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@ftId", ftId);
                    cmd.Parameters.AddWithValue("@grossAmount", grossAmount);
                    cmd.Parameters.AddWithValue("@discountPercentage", 0);
                    cmd.Parameters.AddWithValue("@discountAmount", request.DiscountAmount);
                    cmd.Parameters.AddWithValue("@totalTaxAmount", 0);
                    cmd.Parameters.AddWithValue("@roundOff", 0);
                    cmd.Parameters.AddWithValue("@netAmount", finalNetAmount);
                    cmd.Parameters.AddWithValue("@remarks", DBNull.Value);
                    cmd.Parameters.AddWithValue("@userId", request.UserId);
                    cmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress);
                    cmd.Parameters.AddWithValue("@gstType", DBNull.Value);

                    SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };

                    cmd.Parameters.Add(outParam);
                    await cmd.ExecuteNonQueryAsync();
                }

                txn.Commit();

                return Ok(new
                {
                    success = true,
                    message = "Patient services updated successfully",
                    patientId,
                    uhid,
                    visitId,
                    ftId,
                    labNo = commonLabNo,
                    grossAmount,
                    discountAmount = request.DiscountAmount,
                    netAmount = finalNetAmount,
                    totalPaidAmount
                });
            }
            catch (Exception ex)
            {
                txn.Rollback();

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }


        [HttpGet("get-patient-investigation-details")]
        public async Task<IActionResult> GetPatientInvestigationDetails(
        [FromQuery] string branchId,
        [FromQuery] string? uhid = null,
        [FromQuery] string? labNo = null,
        [FromQuery] string? visitId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(branchId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "BranchId is required"
                    });
                }

                DataTable dt = new DataTable();

                using SqlConnection con = new SqlConnection(
                    _config.GetConnectionString("DefaultConnection")
                );

                using SqlCommand cmd = new SqlCommand("S_GetPatientInvestigationDetails", con);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.Parameters.AddWithValue("@uhid", string.IsNullOrWhiteSpace(uhid) ? DBNull.Value : uhid);
                cmd.Parameters.AddWithValue("@labNo", string.IsNullOrWhiteSpace(labNo) ? DBNull.Value : labNo);
                cmd.Parameters.AddWithValue("@visitId", string.IsNullOrWhiteSpace(visitId) ? DBNull.Value : visitId);

                using SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt);

                var data = dt.AsEnumerable().Select(row => new
                {
                    UHID = row["UHID"]?.ToString(),
                    VisitNo = row["VisitNo"]?.ToString(),
                    LabNo = row["LabNo"]?.ToString(),
                    PatientName = row["PatientName"]?.ToString(),
                    CurrentAge = row["CurrentAge"]?.ToString(),
                    Gender = row["Gender"]?.ToString(),
                    BarCode = row["BarCode"]?.ToString(),
                    Name = row["Name"]?.ToString(),
                    BillDate = row["BillDate"]?.ToString(),

                    PatientInvestigationId = row["PatientInvestigationId"] == DBNull.Value ? 0 : Convert.ToInt32(row["PatientInvestigationId"]),
                    InvestigationId = row["InvestigationId"] == DBNull.Value ? 0 : Convert.ToInt32(row["InvestigationId"]),
                    ReportTypeId = row["ReportTypeId"] == DBNull.Value ? 0 : Convert.ToInt32(row["ReportTypeId"]),

                    IsResultDone = row["IsResultDone"] == DBNull.Value ? 0 : Convert.ToInt32(row["IsResultDone"]),
                    IsReportApproved = row["IsReportApproved"] == DBNull.Value ? 0 : Convert.ToInt32(row["IsReportApproved"]),
                    IsUrgent = row["isUrgent"] == DBNull.Value ? 0 : Convert.ToInt32(row["isUrgent"]),

                    TestRemark = row["TestRemark"]?.ToString()
                }).ToList();

                int totalCount = dt.Rows.Count;

                return Ok(new
                {
                    success = true,
                    message = "Data fetched successfully",
                    totalCount = totalCount,   // ✅ total count
                    count = data.Count,        // (same but kept for compatibility)
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("test-requisition-form")]
        public async Task<IActionResult> GetTestRequisitionForm(
        [FromQuery] int filter,
        [FromQuery] string mode = "view")
        {
            try
            {
                var data = new List<dynamic>();

                using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand("getTestRequisitionForm", con);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@filter", filter);

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    data.Add(new
                    {
                        UHID = reader["UHID"]?.ToString(),
                        PatientName = reader["PatientName"]?.ToString(),
                        Age = reader["Age"]?.ToString(),
                        Gender = reader["Gender"]?.ToString(),
                        Address = reader["Address"]?.ToString(),
                        Contact = reader["ContactNumber"]?.ToString(),
                        ServiceName = reader["ServiceName"]?.ToString(),
                        Client = reader["ClientName"]?.ToString(),
                        BillDate = reader["BillDate"]?.ToString(),
                        SubSubCategory = reader["SubSubCategoryName"]?.ToString(),
                        SampleType = reader["SampleType"]?.ToString(),
                        Doctor = reader["ReferDoctorName"]?.ToString(),
                        DiagnosticNo = reader["DiagnosticNo"]?.ToString(),
                        VisitId = reader["VisitId"]?.ToString()
                    });
                }

                if (data.Count == 0)
                    return Ok(new { success = false, message = "No data" });

                var first = data[0];

                // 🔥 LOAD TEMPLATE
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TRFTemplate.html");
                string html = System.IO.File.ReadAllText(templatePath);

                // 🔥 BUILD ROWS
                StringBuilder rows = new StringBuilder();
                int i = 1;

                foreach (var item in data)
                {
                    rows.Append($@"
                        <tr>
                            <td>{i}</td>
                            <td>{item.ServiceName}</td>
                            <td>{item.SubSubCategory}</td>
                            <td>{item.SampleType}</td>
                        </tr>");
                    i++;
                }

                // 🔥 REPLACE PLACEHOLDERS
                html = html.Replace("{{UHID}}", first.UHID ?? "")
                        .Replace("{{PATIENT_NAME}}", first.PatientName ?? "")
                        .Replace("{{AGE}}", first.Age ?? "")
                        .Replace("{{GENDER}}", first.Gender ?? "")
                        .Replace("{{CONTACT}}", first.Contact ?? "")
                        .Replace("{{ADDRESS}}", first.Address ?? "")
                        .Replace("{{VISIT_ID}}", first.VisitId ?? "")
                        .Replace("{{DIAGNOSTIC_NO}}", first.DiagnosticNo ?? "")
                        .Replace("{{BILL_DATE}}", first.BillDate ?? "")
                        .Replace("{{CLIENT}}", first.Client ?? "")
                        .Replace("{{DOCTOR}}", first.Doctor ?? "")
                        .Replace("{{TEST_ROWS}}", rows.ToString());

                // 🔥 CONVERT TO PDF
                byte[] pdfBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    iText.Html2pdf.HtmlConverter.ConvertToPdf(html, ms);
                    pdfBytes = ms.ToArray();
                }

                string fileName = $"TRF_{first.UHID}_{first.VisitId}.pdf";

                // ✅ MODE: PDF DOWNLOAD
                if (mode == "pdf")
                {
                    return File(pdfBytes, "application/pdf", fileName);
                }

                // ✅ MODE: BASE64
                string base64 = Convert.ToBase64String(pdfBytes);

                return Ok(new
                {
                    success = true,
                    message = "PDF generated",
                    base64,
                    fileName,
                    count = data.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}