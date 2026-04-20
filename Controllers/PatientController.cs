using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;

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

                        // =========================
                        // VALIDATION
                        // =========================
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

                        // =========================
                        // TOTAL CALCULATION
                        // =========================
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

                            // ✅ NEW: after SP insert, override CreatedOn with IST time
                            if (patientId > 0)
                            {
                                var createdOnIST = GetIndianNow();

                                using (SqlCommand createdOnCmd = new SqlCommand(@"
                                UPDATE PatientMaster
                                SET CreatedOn = @CreatedOn
                                WHERE PatientId = @PatientId", con, txn))
                                {
                                    createdOnCmd.Parameters.AddWithValue("@CreatedOn", createdOnIST);
                                    createdOnCmd.Parameters.AddWithValue("@PatientId", patientId);
                                    createdOnCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        string uhid = $"UHID{patientId}";

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

                            // ✅ Update PatientVisitDetails.CreatedOn in IST
                            if (visitId > 0)
                            {
                                DateTime createdOnIST = GetIndianNow();

                                using (SqlCommand visitCreatedOnCmd = new SqlCommand(
                                    @"UPDATE PatientVisitDetails
                                    SET CreatedOn = @CreatedOn
                                    WHERE VisitId = @VisitId", con, txn))
                                {
                                    visitCreatedOnCmd.Parameters.AddWithValue("@CreatedOn", createdOnIST);
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

                        // =========================
                        // STEP 4: SERVICES
                        // =========================
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
                        // STEP 5: RECEIPT
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

                            // ✅ NEW: update Receipts.CreatedOn in IST
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
                        // STEP 6: PAYMENTS
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

                                // ✅ skip invalid payment rows
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

                                    // ✅ update ReceiptsPaymentModeDetails.CreatedOn in IST
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
                                    else
                                    {
                                        // fallback if SP does not return inserted detail ID
                                        using (SqlCommand updateDetailCmd = new SqlCommand(@"
                                        UPDATE ReceiptsPaymentModeDetails
                                        SET CreatedOn = @CreatedOn
                                        WHERE ReceiptID = @ReceiptID
                                        AND PaymentModeId = @PaymentModeId
                                        AND Amount = @Amount
                                        AND CreatedBy = @CreatedBy", con, txn))
                                        {
                                            updateDetailCmd.Parameters.AddWithValue("@CreatedOn", GetIndianNow());
                                            updateDetailCmd.Parameters.AddWithValue("@ReceiptID", receiptId);
                                            updateDetailCmd.Parameters.AddWithValue("@PaymentModeId", paymentModeId);
                                            updateDetailCmd.Parameters.AddWithValue("@Amount", amount);
                                            updateDetailCmd.Parameters.AddWithValue("@CreatedBy", GetInt(model, "UserId"));
                                            updateDetailCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        // =========================
                        // STEP 7: PatientInvestigationDetails
                        // =========================
                        foreach (var s in services.EnumerateArray())
                        {
                            int labNo = 0;

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

                                labNo = Convert.ToInt32(outParamLab.Value);
                            }

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

                            // ✅ Read IsUrgent
                            int isUrgent = 0;
                            if (s.TryGetProperty("IsUrgent", out var urgent1) && urgent1.ValueKind != JsonValueKind.Null)
                                isUrgent = urgent1.GetInt32();
                            else if (s.TryGetProperty("isUrgent", out var urgent2) && urgent2.ValueKind != JsonValueKind.Null)
                                isUrgent = urgent2.GetInt32();

                            // ✅ Read Barcode
                            string? barcode = null;
                            if (s.TryGetProperty("Barcode", out var barcode1) && barcode1.ValueKind != JsonValueKind.Null)
                                barcode = barcode1.GetString();
                            else if (s.TryGetProperty("barcode", out var barcode2) && barcode2.ValueKind != JsonValueKind.Null)
                                barcode = barcode2.GetString();

                            // ✅ Read TestRemark
                            string? testRemark = null;
                            if (s.TryGetProperty("TestRemark", out var remark1) && remark1.ValueKind != JsonValueKind.Null)
                                testRemark = remark1.GetString();
                            else if (s.TryGetProperty("testRemark", out var remark2) && remark2.ValueKind != JsonValueKind.Null)
                                testRemark = remark2.GetString();

                            // ✅ Read ReportingBranchId from root Investigations if needed
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

                            // ✅ NEW: store inserted PatientInvestigationId
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
                                cmd.Parameters.AddWithValue("@labNo", labNo);
                                cmd.Parameters.AddWithValue("@TokenNo", 0);
                                cmd.Parameters.AddWithValue("@userId", GetInt(model, "UserId"));
                                cmd.Parameters.AddWithValue("@isUrgent", isUrgent);
                                cmd.Parameters.AddWithValue("@ReportingBranchId", reportingBranchId > 0 ? reportingBranchId : DBNull.Value);
                                cmd.Parameters.AddWithValue("@Barcode", string.IsNullOrWhiteSpace(barcode) ? DBNull.Value : barcode);
                                cmd.Parameters.AddWithValue("@testRemark", string.IsNullOrWhiteSpace(testRemark) ? DBNull.Value : testRemark);
                                cmd.Parameters.AddWithValue("@sampleTypeId", 0);
                                cmd.Parameters.AddWithValue("@LabComment", DBNull.Value);
                                cmd.Parameters.AddWithValue("@IpAddress", (object?)GetString(model, "IpAddress") ?? DBNull.Value);

                                SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                                {
                                    Direction = ParameterDirection.Output
                                };

                                cmd.Parameters.Add(outParam);
                                cmd.ExecuteNonQuery();

                                // ✅ NEW: get inserted PatientInvestigationId
                                patientInvestigationId = Convert.ToInt32(outParam.Value);

                                Console.WriteLine($"Saved ServiceItemId={investigationId}, Barcode={barcode}, TestRemark={testRemark}");
                            }

                            // ✅ NEW: update CreatedOn in IST for PatientInvestigationDetails
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
    }
}