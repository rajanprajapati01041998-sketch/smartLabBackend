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

        [HttpPost("save-refer-doctor")]
        public async Task<IActionResult> SaveReferDoctor([FromBody] ReferDoctorMasterRequest model)
        {
            try
            {
                int result;

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("IU_ReferDoctorMaster", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@hospId", model.HospId);
                    cmd.Parameters.AddWithValue("@referDoctorId", model.ReferDoctorId);
                    cmd.Parameters.AddWithValue("@title", (object?)model.Title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@name", (object?)model.Name ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@doctorContacNo", string.IsNullOrWhiteSpace(model.DoctorContacNo) ? DBNull.Value : model.DoctorContacNo);
                    cmd.Parameters.AddWithValue("@clinicName", string.IsNullOrWhiteSpace(model.ClinicName) ? DBNull.Value : model.ClinicName);
                    cmd.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(model.Address) ? DBNull.Value : model.Address);
                    cmd.Parameters.AddWithValue("@proId", model.ProId);
                    cmd.Parameters.AddWithValue("@active", model.Active);
                    cmd.Parameters.AddWithValue("@userId", model.UserId);
                    cmd.Parameters.AddWithValue("@IpAddress", string.IsNullOrWhiteSpace(model.IpAddress) ? DBNull.Value : model.IpAddress);

                    var outputParam = new SqlParameter("@Result", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(outputParam);

                    await con.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();

                    result = outputParam.Value == DBNull.Value ? 0 : Convert.ToInt32(outputParam.Value);
                }

                if (result == -1)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "Doctor name already exists",
                        data = result
                    });
                }

                return Ok(new
                {
                    status = true,
                    message = model.ReferDoctorId == 0 ? "Refer doctor saved successfully" : "Refer doctor updated successfully",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "Error while saving refer doctor",
                    data = ex.Message
                });
            }
        }

    }
}