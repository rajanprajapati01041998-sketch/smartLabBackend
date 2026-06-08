using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LISDBACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PageMasterAppController : ControllerBase
    {
        private readonly IConfiguration _config;

        public PageMasterAppController(IConfiguration config)
        {
            _config = config;
        }


        [HttpPost("save-page-setting")]
        public async Task<IActionResult> SavePageSetting(
    [FromBody] PageMasterAppInsertRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    Result = false,
                    Message = "Invalid Request"
                });
            }

            try
            {
                await using SqlConnection con =
                    new SqlConnection(_config.GetConnectionString("DefaultConnection"));

                await con.OpenAsync();

                using SqlCommand cmd =
                    new SqlCommand("I_PageMasterApp", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@BranchId", request.BranchId);
                cmd.Parameters.AddWithValue("@FieldBoy", request.FieldBoy);
                cmd.Parameters.AddWithValue("@Relation", request.Relation);
                cmd.Parameters.AddWithValue("@MaritalStatus", request.MaritalStatus);
                cmd.Parameters.AddWithValue("@AadharNo", request.AadharNo);
                cmd.Parameters.AddWithValue("@Email", request.Email);
                cmd.Parameters.AddWithValue("@Address", request.Address);

                cmd.Parameters.AddWithValue(
                    "@BackgroundColor",
                    (object?)request.BackgroundColor ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@TextColor",
                    (object?)request.TextColor ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@DarkBackground",
                    (object?)request.DarkBackground ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@DarkText",
                    (object?)request.DarkText ?? DBNull.Value);

                DataTable dt = new DataTable();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                dt.Load(reader);

                return Ok(new
                {
                    Result = true,
                    Data = dt.Rows.Cast<DataRow>()
                        .Select(r => dt.Columns.Cast<DataColumn>()
                        .ToDictionary(
                            c => c.ColumnName,
                            c => r[c]))
                        .ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Result = false,
                    Message = ex.Message,
                    InnerException = ex.InnerException?.Message
                });
            }
        }

        [HttpPost("update-page-setting")]
        public async Task<IActionResult> UpdatePageSetting(
            [FromBody] PageMasterAppRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    Result = false,
                    Message = "Invalid Request"
                });
            }

            try
            {
                await using SqlConnection con =
                    new SqlConnection(
                        _config.GetConnectionString("DefaultConnection"));

                await con.OpenAsync();

                using SqlCommand cmd =
                    new SqlCommand("U_PageMasterApp", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@BranchId", request.BranchId);

                cmd.Parameters.AddWithValue(
                    "@FieldBoy",
                    (object?)request.FieldBoy ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@Relation",
                    (object?)request.Relation ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@MaritalStatus",
                    (object?)request.MaritalStatus ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@AadharNo",
                    (object?)request.AadharNo ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@Email",
                    (object?)request.Email ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@Address",
                    (object?)request.Address ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@BackgroundColor",
                    (object?)request.BackgroundColor ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@TextColor",
                    (object?)request.TextColor ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@DarkBackground",
                    (object?)request.DarkBackground ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@DarkText",
                    (object?)request.DarkText ?? DBNull.Value);

                DataTable dt = new DataTable();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                dt.Load(reader);

                return Ok(new
                {
                    Result = true,
                    Message = "Updated Successfully",

                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Result = false,
                    Message = ex.Message,
                    InnerException = ex.InnerException?.Message
                });
            }
        }


        [HttpGet("get-page-setting")]
        public async Task<IActionResult> GetPageSetting(int branchId)
        {
            try
            {
                await using SqlConnection con =
                    new SqlConnection(_config.GetConnectionString("DefaultConnection"));

                await con.OpenAsync();

                using SqlCommand cmd =
                    new SqlCommand("S_PageMasterApp", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@BranchId", branchId);

                DataTable dt = new DataTable();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                dt.Load(reader);

                if (dt.Rows.Count == 0)
                {
                    return Ok(new
                    {
                        Result = false,
                        Message = "No data found"
                    });
                }

                return Ok(new
                {
                    Result = true,
                    Data = dt.Rows.Cast<DataRow>()
                        .Select(r => dt.Columns.Cast<DataColumn>()
                        .ToDictionary(
                            c => c.ColumnName,
                            c => r[c]))
                        .FirstOrDefault()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Result = false,
                    Message = ex.Message,
                    InnerException = ex.InnerException?.Message
                });
            }
        }
    }


}