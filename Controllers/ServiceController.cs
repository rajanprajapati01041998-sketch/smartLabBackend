using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;
using App.Common;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public partial class ServiceController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ServiceController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Check Duplicate Service for Patient (Same Day)
        /// </summary>
        [HttpGet("CheckDuplicateService")]
        public async Task<IActionResult> CheckDuplicateService(int serviceItemId, int patientId)
        {
            try
            {
                var list = new List<DuplicateServiceModel>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("S_GetDublicateServiceName", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@ServiceItemId", serviceItemId);
                    cmd.Parameters.AddWithValue("@PatientId", patientId);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DuplicateServiceModel
                            {
                                ServiceItemId = Convert.ToInt32(reader["ServiceItemId"]),
                                CreatedDate = reader["CreatedDate"]?.ToString(),
                                UserName = reader["UserName"]?.ToString()
                            });
                        }
                    }
                }

                return Ok(new ApiResponse<List<DuplicateServiceModel>>
                {
                    Success = true,
                    Message = list.Count > 0
                        ? "Duplicate service found for today"
                        : "No duplicate service found",
                    Data = list
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error checking duplicate service",
                    Data = ex.Message
                });
            }
        }


        [HttpGet("GetActiveSampleTypesRaw")]
        public async Task<IActionResult> GetActiveSampleTypesRaw(string sampleTypeIds)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("S_GetActiveSampleTypesByIds", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SampleTypeIds", sampleTypeIds);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        // convert to dynamic list
                        var data = new List<Dictionary<string, object>>();

                        foreach (DataRow row in dt.Rows)
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (DataColumn col in dt.Columns)
                            {
                                dict[col.ColumnName] = row[col];
                            }
                            data.Add(dict);
                        }

                        return Ok(new ApiResponse<object>
                        {
                            Success = true,
                            Message = "Data fetched successfully",
                            Data = data
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error executing SP",
                    Data = ex.Message
                });
            }
        }
    }
}