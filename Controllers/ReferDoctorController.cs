using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;
using App.Common;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReferDoctorController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ReferDoctorController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetReferDoctorList")]
        public async Task<IActionResult> GetReferDoctorList(string doctorName = null)
        {
            try
            {
                var doctorList = new List<ReferDoctorModel>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("S_GetReferDoctorList", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // ✅ FIX HERE
                    cmd.Parameters.AddWithValue("@doctorName", doctorName ?? "");

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            doctorList.Add(new ReferDoctorModel
                            {
                                ReferDoctorId = Convert.ToInt32(reader["ReferDoctorId"]),
                                Title = reader["Title"]?.ToString(),
                                Name = reader["Name"]?.ToString(),
                                DoctorName = reader["DoctorName"]?.ToString(),
                                ContactNo = reader["ContactNo"]?.ToString(),
                                ClinicName = reader["ClinicName"]?.ToString(),
                                Address = reader["Address"]?.ToString(),
                                ProId = reader["ProId"] != DBNull.Value ? Convert.ToInt32(reader["ProId"]) : 0,
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }

                return Ok(new ApiResponse<List<ReferDoctorModel>>
                {
                    Success = true,
                    Message = "Refer doctor list fetched successfully",
                    Data = doctorList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error fetching refer doctor list",
                    Data = ex.Message
                });
            }
        }
    }
}