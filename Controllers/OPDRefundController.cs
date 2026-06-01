using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

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