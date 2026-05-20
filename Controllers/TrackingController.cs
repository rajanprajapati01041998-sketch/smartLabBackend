using App.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TrackingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Phlebo sends
        // POST /api/Tracking/update-location
        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] TrackingUpdateLocationRequest request)
        {
            if (request is null)
                return BadRequest(new { message = "Invalid body" });

            var fieldBoyId = request.FieldBoyId ?? request.UserId;

            if (!fieldBoyId.HasValue || fieldBoyId.Value <= 0)
                return BadRequest(new { message = "fieldBoyId (or userId) is required" });

            var capturedAtUtc = request.CapturedAtUtc ?? DateTime.UtcNow;

            try
            {
                using var con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using var cmd = new SqlCommand(
                    "I_FieldBoyLocationHistory",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int).Value =
                    fieldBoyId.Value;

                var lat = cmd.Parameters.Add("@Latitude", SqlDbType.Decimal);
                lat.Precision = 18;
                lat.Scale = 10;
                lat.Value = request.Latitude;

                var lng = cmd.Parameters.Add("@Longitude", SqlDbType.Decimal);
                lng.Precision = 18;
                lng.Scale = 10;
                lng.Value = request.Longitude;

                var acc = cmd.Parameters.Add("@AccuracyMeters", SqlDbType.Decimal);
                acc.Precision = 18;
                acc.Scale = 2;
                acc.Value = request.AccuracyMeters.HasValue
                    ? request.AccuracyMeters.Value
                    : DBNull.Value;

                cmd.Parameters.Add("@CapturedAtUtc", SqlDbType.DateTime2).Value =
                    capturedAtUtc;

                await con.OpenAsync();

                FieldBoyLocationRow? row = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        row = new FieldBoyLocationRow
                        {
                            Id = Convert.ToInt64(reader["Id"]),
                            FieldBoyId = Convert.ToInt32(reader["FieldBoyId"]),
                            Latitude = Convert.ToDecimal(reader["Latitude"]),
                            Longitude = Convert.ToDecimal(reader["Longitude"]),
                            AccuracyMeters = reader["AccuracyMeters"] == DBNull.Value
                                ? null
                                : Convert.ToDecimal(reader["AccuracyMeters"]),
                            CapturedAtUtc = Convert.ToDateTime(reader["CapturedAtUtc"]),
                            CreatedAtUtc = Convert.ToDateTime(reader["CreatedAtUtc"])
                        };
                    }
                }

                return Ok(new
                {
                    message = "Location updated",
                    serverNowUtc = DateTime.UtcNow.ToString("o"),
                    data = row
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    message = "Database error updating location",
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error updating location",
                    error = ex.Message
                });
            }
        }

        // Admin reads (polls)
        // GET /api/Tracking/latest-location?fieldBoyId=123 (or userId=123)
        [HttpGet("latest-location")]
        public async Task<IActionResult> LatestLocation(
        [FromQuery] int fieldBoyId)
        {
            if (fieldBoyId <= 0)
            {
                return BadRequest(new
                {
                    message = "fieldBoyId is required"
                });
            }

            try
            {
                using var con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using var cmd = new SqlCommand(
                    "S_GetLatestFieldBoyLocation",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int)
                    .Value = fieldBoyId;

                await con.OpenAsync();

                List<FieldBoyLocationRow> locations =
                    new List<FieldBoyLocationRow>();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        locations.Add(new FieldBoyLocationRow
                        {
                            Id = Convert.ToInt64(reader["Id"]),

                            FieldBoyId = Convert.ToInt32(
                                reader["FieldBoyId"]),

                            FieldBoyName =
                                reader["FieldBoyName"] == DBNull.Value
                                ? null
                                : reader["FieldBoyName"]?.ToString(),

                            Latitude = Convert.ToDecimal(
                                reader["Latitude"]),

                            Longitude = Convert.ToDecimal(
                                reader["Longitude"]),

                            AccuracyMeters =
                                reader["AccuracyMeters"] == DBNull.Value
                                ? null
                                : Convert.ToDecimal(
                                    reader["AccuracyMeters"]),

                            CapturedAtUtc = Convert.ToDateTime(
                                reader["CapturedAtUtc"]),

                            CreatedAtUtc = Convert.ToDateTime(
                                reader["CreatedAtUtc"])
                        });
                    }
                }

                return Ok(new
                {
                    message = locations.Count == 0
                        ? "No location yet"
                        : "Latest locations",

                    serverNowUtc = DateTime.UtcNow.ToString("o"),

                    count = locations.Count,

                    data = locations
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    message = "Database error fetching latest location",
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error fetching latest location",
                    error = ex.Message
                });
            }
        }

        // Optional: Admin reads path
        // GET /api/Tracking/location-path?fieldBoyId=123&limit=200 (or userId=123)
        [HttpGet("location-path")]
        public async Task<IActionResult> LocationPath(
    [FromQuery] int fieldBoyId,
    [FromQuery] int limit = 500)
        {
            if (fieldBoyId <= 0)
            {
                return BadRequest(new
                {
                    message = "fieldBoyId is required"
                });
            }

            try
            {
                using var con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using var cmd = new SqlCommand(
                    "S_GetFieldBoyLocationPath",
                    con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int)
                    .Value = fieldBoyId;

                cmd.Parameters.Add("@Limit", SqlDbType.Int)
                    .Value = limit;

                await con.OpenAsync();

                List<FieldBoyLocationRow> locations =
                    new List<FieldBoyLocationRow>();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        locations.Add(new FieldBoyLocationRow
                        {
                            Id = Convert.ToInt64(reader["Id"]),

                            FieldBoyId = Convert.ToInt32(
                                reader["FieldBoyId"]),

                            FieldBoyName =
                                reader["FieldBoyName"] == DBNull.Value
                                ? null
                                : reader["FieldBoyName"]?.ToString(),

                            Latitude = Convert.ToDecimal(
                                reader["Latitude"]),

                            Longitude = Convert.ToDecimal(
                                reader["Longitude"]),

                            AccuracyMeters =
                                reader["AccuracyMeters"] == DBNull.Value
                                ? null
                                : Convert.ToDecimal(
                                    reader["AccuracyMeters"]),

                            CapturedAtUtc = Convert.ToDateTime(
                                reader["CapturedAtUtc"]),

                            CreatedAtUtc = Convert.ToDateTime(
                                reader["CreatedAtUtc"])
                        });
                    }
                }

                return Ok(new
                {
                    message = "Location path",
                    count = locations.Count,
                    data = locations
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error fetching path",
                    error = ex.Message
                });
            }
        }
    }
}
