using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LISDBACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RefundController : ControllerBase
    {
        private readonly IConfiguration _config;

        public RefundController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("get-opd-bill-details-for-refund")]
        public async Task<IActionResult> GetOPDBillDetailsForRefund(
            [FromQuery] string? receiptNo,
            [FromQuery] string? billNo,
            [FromQuery] string? uhid)
        {
            try
            {
                using SqlConnection con = new SqlConnection(
                    _config.GetConnectionString("DefaultConnection"));

                await con.OpenAsync();

                int visitId = 0;

                using (SqlCommand cmd = new SqlCommand("S_GetOPDBillForRefund", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue(
                        "@receiptNo",
                        string.IsNullOrWhiteSpace(receiptNo)
                            ? DBNull.Value
                            : receiptNo);

                    cmd.Parameters.AddWithValue(
                        "@billNo",
                        string.IsNullOrWhiteSpace(billNo)
                            ? DBNull.Value
                            : billNo);

                    cmd.Parameters.AddWithValue(
                        "@uhid",
                        string.IsNullOrWhiteSpace(uhid)
                            ? DBNull.Value
                            : uhid);

                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        visitId = Convert.ToInt32(reader["VisitId"]);
                    }

                    await reader.CloseAsync();
                }

                if (visitId <= 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Visit not found",
                        data = new List<object>()
                    });
                }

                List<Dictionary<string, object?>> data = new();

                using (SqlCommand cmd2 = new SqlCommand("S_GetOPDBillDetailsForRefund", con))
                {
                    cmd2.CommandType = CommandType.StoredProcedure;
                    cmd2.Parameters.AddWithValue("@visitId", visitId);

                    using SqlDataReader reader2 = await cmd2.ExecuteReaderAsync();

                    while (await reader2.ReadAsync())
                    {
                        Dictionary<string, object?> row = new();

                        for (int i = 0; i < reader2.FieldCount; i++)
                        {
                            row[reader2.GetName(i)] =
                                reader2.IsDBNull(i)
                                    ? null
                                    : reader2.GetValue(i);
                        }

                        data.Add(row);
                    }
                }

                return Ok(new
                {
                    success = true,
                    // visitId = visitId,
                    message = "OPD bill details fetched successfully",
                    data = data
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