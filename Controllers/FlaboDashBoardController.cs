using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using log4net;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlaboDashBoardController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private static readonly ILog _log =
            LogManager.GetLogger(typeof(FlaboDashBoardController));

        public FlaboDashBoardController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("sample-summary")]
        public async Task<IActionResult> GetFieldBoySampleSummary(
    [FromQuery] int fieldBoyId,
    [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate)
        {
            if (fieldBoyId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "fieldBoyId is required"
                });
            }

            try
            {
                using var con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using var cmd = new SqlCommand(
                    "S_GetFieldBoySampleSummary",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int).Value =
                    fieldBoyId;

                cmd.Parameters.Add("@FromDate", SqlDbType.Date).Value =
                    fromDate.HasValue
                        ? fromDate.Value.Date
                        : DBNull.Value;

                cmd.Parameters.Add("@ToDate", SqlDbType.Date).Value =
                    toDate.HasValue
                        ? toDate.Value.Date
                        : DBNull.Value;

                await con.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No data found",
                        data = new
                        {
                            fieldBoyId,
                            totalSamples = 0,
                            totalSamplePicked = 0,
                            totalSampleDelivered = 0,
                            totalPaymentCollected = 0
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Sample summary fetched successfully",
                    data = new
                    {
                        fieldBoyId = Convert.ToInt32(reader["FieldBoyId"]),
                        totalSamples = Convert.ToInt32(reader["TotalSamples"]),
                        totalSamplePicked = Convert.ToInt32(reader["TotalSamplePicked"]),
                        totalSampleDelivered = Convert.ToInt32(reader["TotalSampleDelivered"]),
                        totalPaymentCollected = Convert.ToDecimal(reader["TotalPaymentCollected"])
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error("Error fetching sample summary", ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching sample summary",
                    error = ex.Message
                });
            }
        }

        [HttpPut("update-sample-status")]
        public async Task<IActionResult> UpdateSampleStatus(
        [FromBody] UpdateSampleStatusRequest request)
        {
            if (request == null || request.Id <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid request"
                });
            }

            try
            {
                using var con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using var cmd = new SqlCommand(
                    "U_PatientSampleTrackingStatus",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = request.Id;

                cmd.Parameters.Add("@SamplePickup", SqlDbType.Bit).Value =
                    request.SamplePickup.HasValue
                        ? request.SamplePickup.Value
                        : DBNull.Value;

                cmd.Parameters.Add("@SampleDelivered", SqlDbType.Bit).Value =
                    request.SampleDelivered.HasValue
                        ? request.SampleDelivered.Value
                        : DBNull.Value;


                await con.OpenAsync();

                await cmd.ExecuteNonQueryAsync();

                string message = "Sample status updated successfully";

                if (request.SamplePickup == true)
                {
                    message = "Sample picked successfully";
                }

                if (request.SampleDelivered == true)
                {
                    message = "Sample delivered successfully";
                }

                return Ok(new
                {
                    success = true,
                    message
                });
            }
            catch (Exception ex)
            {
                _log.Error("Update sample status error", ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error updating sample status",
                    error = ex.Message
                });
            }
        }
    }

}