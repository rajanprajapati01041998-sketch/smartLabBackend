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
        [FromQuery] string? loginBranchIdList,
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

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int).Value = fieldBoyId;

                cmd.Parameters.Add("@LoginBranchIdList", SqlDbType.NVarChar).Value =
                    string.IsNullOrWhiteSpace(loginBranchIdList)
                        ? DBNull.Value
                        : loginBranchIdList;

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
                        data = (object?)null
                    });
                }

                var data = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    data[reader.GetName(i)] =
                        reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                }

                return Ok(new
                {
                    success = true,
                    message = "Sample summary fetched successfully",
                    data
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


        [HttpPost("patient-sample-tracking")]
        public async Task<IActionResult> GetPatientSampleTracking(
        [FromBody] PatientSampleTrackingRequest request)
        {
            try
            {
                using var con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using var cmd = new SqlCommand(
                    "S_GetPatientSampleTracking",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int).Value =
                    request.FieldBoyId;

                cmd.Parameters.Add("@LoginBranchIdList", SqlDbType.NVarChar).Value =
                    string.IsNullOrWhiteSpace(request.LoginBranchIdList)
                        ? DBNull.Value
                        : request.LoginBranchIdList;

                cmd.Parameters.Add("@FromDate", SqlDbType.Date).Value =
                    request.FromDate.HasValue
                        ? request.FromDate.Value.Date
                        : DBNull.Value;

                cmd.Parameters.Add("@ToDate", SqlDbType.Date).Value =
                    request.ToDate.HasValue
                        ? request.ToDate.Value.Date
                        : DBNull.Value;

                cmd.Parameters.Add("@UHID", SqlDbType.NVarChar).Value =
                    string.IsNullOrWhiteSpace(request.UHID)
                        ? DBNull.Value
                        : request.UHID;

                cmd.Parameters.Add("@PatientName", SqlDbType.NVarChar).Value =
                    string.IsNullOrWhiteSpace(request.PatientName)
                        ? DBNull.Value
                        : request.PatientName;

                await con.OpenAsync();

                var data = new List<Dictionary<string, object?>>();

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);

                        if (columnName == "ServiceName")
                        {
                            var serviceNames = reader.IsDBNull(i)
                                ? ""
                                : reader.GetValue(i).ToString();

                            row["ServiceList"] = string.IsNullOrWhiteSpace(serviceNames)
                                ? new List<object>()
                                : serviceNames
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select((x, index) => new
                                    {
                                        id = index + 1,
                                        serviceName = x.Trim()
                                    })
                                    .ToList();

                            continue;
                        }
                        row[columnName] =
                            reader.IsDBNull(i)
                                ? null
                                : reader.GetValue(i);
                    }
                    data.Add(row);
                }

                return Ok(new
                {
                    success = true,
                    message = "Patient sample tracking fetched successfully",
                    data
                });
            }
            catch (Exception ex)
            {
                _log.Error("Error fetching patient sample tracking", ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching patient sample tracking",
                    error = ex.Message
                });
            }
        }

        [HttpPost("update-sample-status")]
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