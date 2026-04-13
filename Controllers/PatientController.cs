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

                        object DbVal(JsonElement el) =>
                            el.ValueKind == JsonValueKind.Null ? DBNull.Value :
                            el.ValueKind == JsonValueKind.Undefined ? DBNull.Value :
                            el.ToString();

                        // =========================
                        // TOTAL CALCULATION
                        // =========================
                        decimal totalService = 0;
                        decimal totalPayment = 0;

                        if (model.TryGetProperty("Services", out JsonElement services))
                        {
                            foreach (var s in services.EnumerateArray())
                            {
                                decimal amount = s.GetProperty("Amount").GetDecimal();
                                int qty = s.TryGetProperty("qty", out var q) ? q.GetInt32() : 1;

                                totalService += amount * qty;
                            }
                        }

                        if (model.TryGetProperty("payments", out JsonElement payments))
                        {
                            foreach (var p in payments.EnumerateArray())
                            {
                                totalPayment += p.GetProperty("amount").GetDecimal();
                            }
                        }

                        // =========================
                        // STEP 1: PATIENT
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("IU_PatientMaster", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                            cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@patientId", 0);
                            cmd.Parameters.AddWithValue("@uhid", DBNull.Value);

                            cmd.Parameters.AddWithValue("@title", DbVal(model.GetProperty("Title")));
                            cmd.Parameters.AddWithValue("@firstName", DbVal(model.GetProperty("FirstName")));
                            cmd.Parameters.AddWithValue("@middleName", DbVal(model.GetProperty("MiddleName")));
                            cmd.Parameters.AddWithValue("@lastName", DbVal(model.GetProperty("LastName")));

                            cmd.Parameters.AddWithValue("@ageYears", model.GetProperty("AgeYears").GetInt32());
                            cmd.Parameters.AddWithValue("@ageMonths", model.GetProperty("AgeMonths").GetInt32());
                            cmd.Parameters.AddWithValue("@ageDays", model.GetProperty("AgeDays").GetInt32());
                            var dobProperty = model.GetProperty("DOB");

                            if (dobProperty.ValueKind == JsonValueKind.String &&
                                !string.IsNullOrWhiteSpace(dobProperty.GetString()) &&
                                DateTime.TryParse(dobProperty.GetString(), out DateTime dob))
                            {
                                cmd.Parameters.AddWithValue("@dob", dob);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@dob", DBNull.Value);
                            }
                            cmd.Parameters.AddWithValue("@gender", DbVal(model.GetProperty("Gender")));
                            cmd.Parameters.AddWithValue("@maritalStatus", DbVal(model.GetProperty("MaritalStatus")));
                            cmd.Parameters.AddWithValue("@relation", DbVal(model.GetProperty("Relation")));
                            cmd.Parameters.AddWithValue("@relativeName", DbVal(model.GetProperty("RelativeName")));

                            cmd.Parameters.AddWithValue("@aadharNumber", DBNull.Value);
                            cmd.Parameters.AddWithValue("@idProofName", DBNull.Value);
                            cmd.Parameters.AddWithValue("@idProofNumber", DBNull.Value);

                            cmd.Parameters.AddWithValue("@selfContactNumber", DbVal(model.GetProperty("ContactNumber")));
                            cmd.Parameters.AddWithValue("@emergencyContactNumber", DBNull.Value);
                            cmd.Parameters.AddWithValue("@email", DBNull.Value);
                            cmd.Parameters.AddWithValue("@privilegedCardNumber", DBNull.Value);

                            cmd.Parameters.AddWithValue("@address", DbVal(model.GetProperty("Address")));

                            cmd.Parameters.AddWithValue("@countryId", model.GetProperty("CountryId").GetInt32());
                            cmd.Parameters.AddWithValue("@country", DbVal(model.GetProperty("Country")));

                            cmd.Parameters.AddWithValue("@stateId", model.GetProperty("StateId").GetInt32());
                            cmd.Parameters.AddWithValue("@state", DbVal(model.GetProperty("State")));

                            cmd.Parameters.AddWithValue("@districtId", model.GetProperty("DistrictId").GetInt32());
                            cmd.Parameters.AddWithValue("@district", DbVal(model.GetProperty("District")));

                            cmd.Parameters.AddWithValue("@cityId", model.GetProperty("CityId").GetInt32());
                            cmd.Parameters.AddWithValue("@city", DbVal(model.GetProperty("City")));

                            cmd.Parameters.AddWithValue("@insuranceCompanyId", 0);
                            cmd.Parameters.AddWithValue("@corporateId", 0);

                            cmd.Parameters.AddWithValue("@cardNo", DBNull.Value);
                            cmd.Parameters.AddWithValue("@patientImagePath", DBNull.Value);

                            cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                            cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));
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
                                // 👇 Add invisible character instead of visible suffix
                                string invisibleChar = "\u200B"; // zero-width space

                                string newMiddleName = (originalMiddleName ?? "") + invisibleChar;

                                cmd.Parameters["@middleName"].Value = newMiddleName;

                                cmd.ExecuteNonQuery();
                                patientId = Convert.ToInt32(output.Value);
                            }
                        }

                        string uhid = $"UHID{patientId}";

                        // =========================
                        // STEP 2: VISIT
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("I_PatientVisitDetails", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            // Basic Details
                            cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                            cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@patientId", patientId);
                            cmd.Parameters.AddWithValue("@uhid", uhid);

                            // Visit Info
                            cmd.Parameters.AddWithValue("@type", "OPD");
                            cmd.Parameters.AddWithValue("@typeId", 1);
                            cmd.Parameters.AddWithValue("@currentAge",
                                model.GetProperty("AgeYears").GetInt32() + "Y");

                            // Doctor Info (FIXED)
                            cmd.Parameters.AddWithValue("@doctorId",
                                model.TryGetProperty("DoctorId", out var doctor) &&
                                doctor.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? doctor.GetInt32()
                                    : (object)DBNull.Value
                            );

                            cmd.Parameters.AddWithValue("@referDoctorId",
                                model.TryGetProperty("ReferDoctorId", out var refDoctor)
                                    ? refDoctor.GetInt32()
                                    : (object)DBNull.Value);

                            // Corporate / Insurance
                            cmd.Parameters.AddWithValue("@corporateId", model.GetProperty("CorporateId").GetInt32());
                            cmd.Parameters.AddWithValue("@insuranceCompanyId", 0);

                            // Billing
                            cmd.Parameters.AddWithValue("@totalBillAmount", totalService);
                            cmd.Parameters.AddWithValue("@totalPaidAmount", totalPayment);
                            cmd.Parameters.AddWithValue("@totalBalanceAmount", totalService - totalPayment);
                            cmd.Parameters.AddWithValue("@totalPayableAmount", totalService);

                            // User Info
                            cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                            cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));
                            cmd.Parameters.AddWithValue("@uniqueId", Guid.NewGuid().ToString("N"));

                            // ✅ New Fields You Wanted
                            cmd.Parameters.AddWithValue("@referLabId",
                                model.TryGetProperty("ReferLabId", out var refLab)
                                    ? refLab.GetInt32()
                                    : 0);

                            cmd.Parameters.AddWithValue("@visitTypeId",
                                model.TryGetProperty("VisitTypeId", out var visitType)
                                    ? visitType.GetInt32()
                                    : 0);

                            cmd.Parameters.AddWithValue("@fieldBoyId",
                                model.TryGetProperty("FieldBoyId", out var fieldBoy)
                                    ? fieldBoy.GetInt32()
                                    : 0);

                            cmd.Parameters.AddWithValue("@CollectionDateTime",
                                model.TryGetProperty("CollectionDateTime", out var collectionDT) &&
                                !string.IsNullOrWhiteSpace(collectionDT.ToString()) &&
                                DateTime.TryParse(collectionDT.ToString(), out DateTime parsedDate)
                                    ? parsedDate
                                    : (object)DBNull.Value
                            );

                            // Optional Fields (safe defaults)
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

                            // Output Parameter
                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };
                            cmd.Parameters.Add(outParam);

                            // Execute
                            cmd.ExecuteNonQuery();

                            visitId = Convert.ToInt32(outParam.Value);
                        }

                        // =========================
                        // STEP 3: FINANCIAL
                        // =========================
                        using (SqlCommand cmd = new SqlCommand("I_FinancialTransactions", con, txn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                            cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@visitId", visitId);
                            cmd.Parameters.AddWithValue("@patientId", patientId);

                            cmd.Parameters.AddWithValue("@tnxType", "OPD");
                            cmd.Parameters.AddWithValue("@tnxTypeId", 1);

                            cmd.Parameters.AddWithValue("@grossAmount", totalService);
                            cmd.Parameters.AddWithValue("@discountAmount", 0);
                            cmd.Parameters.AddWithValue("@netAmount", totalService);

                            cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                            cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));
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

                        foreach (var s in model.GetProperty("Services").EnumerateArray())
                        {
                            using (SqlCommand cmd = new SqlCommand("I_FinancialTransactionDetails", con, txn))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;

                                int qty = s.TryGetProperty("qty", out var q) ? q.GetInt32() : 1;
                                decimal rate = s.GetProperty("Amount").GetDecimal();
                                decimal total = qty * rate;

                                int serviceItemId = s.GetProperty("ServiceItemId").GetInt32();

                                cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                                cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                                cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                                cmd.Parameters.AddWithValue("@FTID", financialId);
                                cmd.Parameters.AddWithValue("@visitId", visitId);
                                cmd.Parameters.AddWithValue("@patientId", patientId);
                                cmd.Parameters.AddWithValue("@corporateId",
                                        model.TryGetProperty("CorporateId", out var corp) && corp.ValueKind != JsonValueKind.Null
                                            ? corp.GetInt32()
                                            : 0
                                    );
                                cmd.Parameters.AddWithValue("@serviceItemId", serviceItemId);
                                cmd.Parameters.AddWithValue("@subSubCategoryId", s.GetProperty("SubSubCategoryId").GetInt32());
                                cmd.Parameters.AddWithValue("@serviceName", s.GetProperty("ServiceName").GetString() ?? "");

                                cmd.Parameters.AddWithValue("@rate", rate);
                                cmd.Parameters.AddWithValue("@qty", qty);
                                cmd.Parameters.AddWithValue("@grossAmt", total);
                                cmd.Parameters.AddWithValue("@netAmt", total);

                                cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                                cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));

                                // IMPORTANT: capture inserted FTDId
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

                            cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                            cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                            cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                            cmd.Parameters.AddWithValue(parameterName: "@FTID", financialId);
                            cmd.Parameters.AddWithValue("@visitId", visitId);
                            cmd.Parameters.AddWithValue("@patientId", patientId);

                            cmd.Parameters.AddWithValue("@amount", totalPayment);

                            cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                            cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));
                            cmd.Parameters.AddWithValue("@uniqueId", Guid.NewGuid().ToString("N"));

                            SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };

                            cmd.Parameters.Add(outParam);
                            cmd.ExecuteNonQuery();

                            receiptId = Convert.ToInt32(outParam.Value);
                        }

                        // =========================
                        // STEP 6: PAYMENTS
                        // =========================
                        foreach (var p in model.GetProperty("payments").EnumerateArray())
                        {
                            using (SqlCommand cmd = new SqlCommand("I_ReceiptsPaymentModeDetails", con, txn))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;

                                cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                                cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                                cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                                cmd.Parameters.AddWithValue("@receiptID", receiptId);

                                cmd.Parameters.AddWithValue("@amount", p.GetProperty("amount").GetDecimal());
                                cmd.Parameters.AddWithValue("@paymentModeId", p.GetProperty("paymentModeId").GetInt32());

                                cmd.Parameters.AddWithValue("@ChequeDate", DBNull.Value);
                                cmd.Parameters.AddWithValue("@bankId", p.TryGetProperty("bankId", out var bankIdProp) ? bankIdProp.GetInt32() : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@referenceNo", p.TryGetProperty("referenceNo", out var refNoProp) ? refNoProp.GetString() : (object)DBNull.Value);

                                cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                                cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));

                                cmd.ExecuteNonQuery();
                            }
                        }

                        // =========================
                        // STEP 7: PatientInvestigationDetails
                        // =========================
                        if (model.TryGetProperty("Services", out JsonElement Services))
                        {
                            foreach (var s in services.EnumerateArray())
                            {
                                int labNo = 0;

                                using (SqlCommand labCmd = new SqlCommand("getLabNo", con, txn))
                                {
                                    labCmd.CommandType = CommandType.StoredProcedure;
                                    labCmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());

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
                                {
                                    investigationId = sid.GetInt32();
                                }
                                else if (s.TryGetProperty("serviceItemId", out var sid2) && sid2.ValueKind != JsonValueKind.Null)
                                {
                                    investigationId = sid2.GetInt32();
                                }
                                else if (s.TryGetProperty("serviceitemid", out var sid3) && sid3.ValueKind != JsonValueKind.Null)
                                {
                                    investigationId = sid3.GetInt32();
                                }

                                // find correct FTDId for this service
                                int matchedFtdId = serviceFtdList
                                    .FirstOrDefault(x => x.ServiceItemId == investigationId)
                                    .FTDId;

                                if (matchedFtdId <= 0)
                                {
                                    throw new Exception($"FTDId not found for investigation/service item id {investigationId}");
                                }

                                using (SqlCommand cmd = new SqlCommand("I_PatientInvestigationDetails", con, txn))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;

                                    cmd.Parameters.AddWithValue("@hospId", model.GetProperty("HospId").GetInt32());
                                    cmd.Parameters.AddWithValue("@branchId", model.GetProperty("BranchId").GetInt32());
                                    cmd.Parameters.AddWithValue("@loginBranchId", model.GetProperty("LoginBranchId").GetInt32());
                                    cmd.Parameters.AddWithValue("@visitId", visitId);

                                    // CORRECT VALUE
                                    cmd.Parameters.AddWithValue("@FTDID", matchedFtdId);

                                    cmd.Parameters.AddWithValue("@investigationId", investigationId);

                                    cmd.Parameters.AddWithValue("@doctorId",
                                        model.TryGetProperty("DoctorId", out var doc) && doc.ValueKind != JsonValueKind.Null
                                            ? doc.GetInt32()
                                            : 0);

                                    cmd.Parameters.AddWithValue("@patientId", patientId);
                                    cmd.Parameters.AddWithValue("@labNo", labNo);
                                    cmd.Parameters.AddWithValue("@TokenNo", 0);
                                    cmd.Parameters.AddWithValue("@userId", model.GetProperty("UserId").GetInt32());
                                    cmd.Parameters.AddWithValue("@isUrgent", 0);
                                    cmd.Parameters.AddWithValue("@ReportingBranchId", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Barcode", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@testRemark", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@sampleTypeId", 0);
                                    cmd.Parameters.AddWithValue("@LabComment", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IpAddress", DbVal(model.GetProperty("IpAddress")));

                                    SqlParameter outParam = new SqlParameter("@Result", SqlDbType.Int)
                                    {
                                        Direction = ParameterDirection.Output
                                    };

                                    cmd.Parameters.Add(outParam);
                                    cmd.ExecuteNonQuery();
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
                        return StatusCode(500, new { success = false, message = ex.Message });
                    }
                }
            }
        }

        // get patient by uhid
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
                                        ? null
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