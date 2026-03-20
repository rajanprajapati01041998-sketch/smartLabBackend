using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;
using App.Common;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FieldBoyController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public FieldBoyController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Get Field Boy List
        /// </summary>
        [HttpGet("GetFieldBoyList")]
        public async Task<IActionResult> GetFieldBoyList()
        {
            try
            {
                var fieldBoyList = new List<FieldBoyModel>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand("S_getFieldBoyMaster", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        await con.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                fieldBoyList.Add(new FieldBoyModel
                                {
                                    FieldBoyId = Convert.ToInt32(reader["FieldBoyId"]),
                                    FieldBoyName = reader["FieldBoyName"]?.ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"])
                                });
                            }
                        }
                    }
                }

                return Ok(new ApiResponse<List<FieldBoyModel>>
                {
                    Success = true,
                    Message = "Field boy list fetched successfully",
                    Data = fieldBoyList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error fetching field boy list",
                    Data = ex.Message
                });
            }
        }
    }

    // ✅ Model
    public class FieldBoyModel
    {
        public int FieldBoyId { get; set; }
        public string FieldBoyName { get; set; }
        public bool IsActive { get; set; }
    }

    // ✅ Standard Response Wrapper

}