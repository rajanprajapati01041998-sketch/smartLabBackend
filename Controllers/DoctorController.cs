using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using App.Models;
using App.Common;
using log4net;


namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(typeof(DoctorController));

        public DoctorController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Get Doctor List from Stored Procedure
        /// </summary>
        /// <param name="branchId">Branch Id</param>
        /// <param name="filter">Optional SQL Filter (Use carefully)</param>
        /// <returns>List of Doctors</returns>
        [HttpGet("GetDoctorList")]
        public async Task<IActionResult> GetDoctorList(int branchId, string filter = null)
        {
            try
            {
                _log.Info($"GetDoctorList API called. branchId={branchId}, filter={filter}");

                var doctorList = new List<DoctorModel>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand("getDoctorList", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Parameters
                        cmd.Parameters.AddWithValue("@branchId", branchId);
                        cmd.Parameters.AddWithValue("@filter", (object)filter ?? DBNull.Value);

                        await con.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                doctorList.Add(new DoctorModel
                                {
                                    DoctorId = Convert.ToInt32(reader["DoctorId"]),
                                    Name = reader["Name"]?.ToString(),
                                    Department = reader["Department"]?.ToString()
                                });
                            }
                        }
                    }
                }
                _log.Info($"GetDoctorList success.");
                return Ok(new ApiResponse<List<DoctorModel>>

                {
                    Success = true,
                    Message = "Doctor list fetched successfully",
                    Data = doctorList
                });

            }
            catch (Exception ex)
            {
                _log.Error("Unhandled error in GetDashboardStates API", ex);

                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error fetching doctor list",
                    Data = ex.Message
                });
            }
        }
    }

    // ✅ Doctor Model
    public class DoctorModel
    {
        public int DoctorId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
    }

    // ✅ Standard API Response Model

}