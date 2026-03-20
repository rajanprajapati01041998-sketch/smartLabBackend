using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;
using App.Common;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OutSourceLabController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public OutSourceLabController(IConfiguration configuration)
        {
            _configuration = configuration;
        }



        [HttpPost("CreateOutSourceLab")]
        public async Task<IActionResult> CreateOutSourceLab([FromBody] OutSourceLabModel model)
        {
            try
            {
                int newId = 0;

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(@"
            INSERT INTO OutSourceLabMaster
            (
                HospId,
                BranchId,
                OutSourceLab,
                ContactPerson,
                ContactNumber,
                Address,
                IsActive,
                CreatedBy,
                CreatedOn,
                IpAddress,
                UniqueId
            )
            VALUES
            (
                @HospId,
                @BranchId,
                @OutSourceLab,
                @ContactPerson,
                @ContactNumber,
                @Address,
                @IsActive,
                @CreatedBy,
                GETDATE(),
                @IpAddress,
                @UniqueId
            );
            SELECT SCOPE_IDENTITY();
        ", con))
                {
                    cmd.CommandType = CommandType.Text;

                    _ = cmd.Parameters.AddWithValue("@HospId", model.HospId);
                    cmd.Parameters.AddWithValue("@BranchId", model.BranchId);
                    cmd.Parameters.AddWithValue("@OutSourceLab", model.OutSourceLab);
                    cmd.Parameters.AddWithValue("@ContactPerson", (object)model.ContactPerson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ContactNumber", (object)model.ContactNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", (object)model.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedBy", model.CreatedBy);
                    cmd.Parameters.AddWithValue("@IpAddress", (object)model.IpAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UniqueId", Guid.NewGuid().ToString("N"));

                    await con.OpenAsync();

                    var result = await cmd.ExecuteScalarAsync();
                    newId = Convert.ToInt32(result);
                }

                return Ok(new ApiResponse<int>
                {
                    Success = true,
                    Message = "OutSource Lab created successfully",
                    Data = newId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error creating OutSource Lab",
                    Data = ex.Message
                });
            }
        }

        /// <summary>
        /// Get OutSource Lab List (Active / Inactive)
        /// </summary>
        [HttpGet("GetOutSourceLabList")]
        public async Task<IActionResult> GetOutSourceLabList(string activeStatus)
        {
            try
            {
                var labList = new List<OutSourceLabModel>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("S_getOutSourceLabMasterList", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // ✅ FIX: handle blank/null
                    cmd.Parameters.AddWithValue("@activeStatus",
                        string.IsNullOrEmpty(activeStatus) ? "1,0" : activeStatus);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            labList.Add(new OutSourceLabModel
                            {
                                OutSourceLabId = Convert.ToInt32(reader["OutSourceLabId"]),
                                OutSourceLab = reader["OutSourceLab"]?.ToString(),
                                BranchId = Convert.ToInt32(reader["BranchId"]),
                                BranchName = reader["BranchName"]?.ToString(),
                                ContactPerson = reader["ContactPerson"]?.ToString(),
                                ContactNumber = reader["ContactNumber"]?.ToString(),
                                Address = reader["Address"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }

                return Ok(new ApiResponse<List<OutSourceLabModel>>
                {
                    Success = true,
                    Message = "OutSource Lab list fetched successfully",
                    Data = labList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error fetching OutSource Lab list",
                    Data = ex.Message
                });
            }
        }
    }


}