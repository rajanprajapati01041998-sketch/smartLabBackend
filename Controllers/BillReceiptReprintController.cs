using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LISDBACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BillReceiptReprintController : ControllerBase
    {
        private readonly IConfiguration _config;

        public BillReceiptReprintController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("get-bill-receipt-reprint")]
        public async Task<IActionResult> GetBillReceiptReprint(
            [FromBody] BillReceiptReprintRequest request)
        {
            try
            {
                await using SqlConnection con =
                    new SqlConnection(
                        _config.GetConnectionString("DefaultConnection"));
                await con.OpenAsync();
                using SqlCommand cmd =
                    new SqlCommand("S_GetBillReceiptReprintDetails", con);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue(
                    "@branchIdList",
                    (object?)request.BranchIdList ?? DBNull.Value);
                cmd.Parameters.AddWithValue(
                    "@UHID",
                    (object?)request.UHID ?? "");
                cmd.Parameters.AddWithValue(
                    "@Name",
                    (object?)request.Name ?? "");
                cmd.Parameters.AddWithValue(
                    "@Type",
                    request.Type);
                cmd.Parameters.AddWithValue(
                    "@BillNo",
                    (object?)request.BillNo ?? "");
                cmd.Parameters.AddWithValue(
                    "@ReceiptNo",
                    (object?)request.ReceiptNo ?? "");
                cmd.Parameters.AddWithValue(
                    "@FromDate",
                    (object?)request.FromDate ?? "");
                cmd.Parameters.AddWithValue(
                    "@Todate",
                    (object?)request.ToDate ?? "");
                DataTable dt = new DataTable();
                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();
                dt.Load(reader);
                var data = dt.Rows.Cast<DataRow>()
                    .Select(r => dt.Columns.Cast<DataColumn>()
                    .ToDictionary(
                        c => c.ColumnName,
                        c => r[c] == DBNull.Value ? null : r[c]
                    ))
                    .ToList();
                return Ok(new
                {
                    Result = true,
                    Count = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Result = false,
                    Message = ex.Message,
                    InnerException = ex.InnerException?.Message
                });
            }
        }
    }
}