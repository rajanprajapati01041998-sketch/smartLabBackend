using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using LISD.Models;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LocationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetLocations")]
        public IActionResult GetLocations(int parentId, string? filter = null)
        {
            List<LocationMaster> locations = new List<LocationMaster>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand("S_LocationMaster", con);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@parentId", parentId);
                cmd.Parameters.AddWithValue("@filter", filter is null ? DBNull.Value : filter);

                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    locations.Add(new LocationMaster
                    {
                        LocationId = Convert.ToInt32(reader["LocationId"]),
                        LocationName = reader["LocationName"].ToString(),
                        IsParent = Convert.ToBoolean(reader["IsParent"]),
                        ParentId = Convert.ToInt32(reader["ParentId"]),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        Pincode = reader["Pincode"].ToString(),
                        IsState = Convert.ToBoolean(reader["IsState"])
                    });
                }
            }

            return Ok(locations);
        }


        [HttpGet("getBranchLocationById_flabo")]
        public async Task<IActionResult> TestBranchLocation([FromQuery] int branchId)
        {
            try
            {
                using SqlConnection con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection")
                );

                using SqlCommand cmd = new SqlCommand("dbo.S_GetBranchLocationByBranchIdForFlabo", con);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                var result = new List<Dictionary<string, object?>>();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }

                    result.Add(row);
                }

                return Ok(new
                {
                    status = true,
                    message = "Success",
                    count = result.Count,
                    data = result
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "SQL Error",
                    error = ex.Message,
                    procedure = ex.Procedure,
                    line = ex.LineNumber
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "API Error",
                    error = ex.Message
                });
            }
        }



    }
}
