using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;

[Route("api/[controller]")]
[ApiController]
public class FlaboLoginController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IPAddressService _ipService;

    public FlaboLoginController(
        IConfiguration config,
        IPAddressService ipService)
    {
        _config = config;
        _ipService = ipService;
    }

    // =========================
    // 1. Get Branch List
    // =========================
    [HttpPost("fieldBoyBranchList")]
    public async Task<IActionResult> FieldBoyBranchList(
        [FromBody] FieldBoyLoginRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.UserIdApp) ||
            string.IsNullOrWhiteSpace(request.PasswordApp))
        {
            return BadRequest(new
            {
                success = false,
                message = "UserIdApp and PasswordApp are required"
            });
        }

        try
        {
            using var con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection"));

            using var cmd = new SqlCommand(
                "S_GetFieldBoyWiseBranchListForLogin",
                con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("@userIdApp", SqlDbType.NVarChar, 100)
                .Value = request.UserIdApp;

            cmd.Parameters.Add("@passwordApp", SqlDbType.NVarChar, 100)
                .Value = request.PasswordApp;

            await con.OpenAsync();

            var branches = new List<object>();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int isExist = Convert.ToInt32(reader["isExist"]);

                if (isExist != 1)
                {
                    return Ok(new
                    {
                        success = false,
                        isExist,
                        message = isExist == 0
                            ? "Invalid User ID or Password"
                            : "No branch mapped with this field boy",
                        data = branches
                    });
                }

                branches.Add(new
                {
                    branchId = Convert.ToInt32(reader["BranchId"]),
                    branchCode = reader["BranchCode"]?.ToString(),
                    branchName = reader["BranchName"]?.ToString(),
                    fullBranchName = reader["FullBranchName"]?.ToString()
                });
            }

            return Ok(new
            {
                success = true,
                message = "Branch list fetched successfully",
                count = branches.Count,
                data = branches
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error fetching branch list",
                error = ex.Message
            });
        }
    }

    // =========================
    // 2. Field Boy Login
    // =========================
    // =========================
    // 2. Field Boy Login
    // =========================
    [HttpPost("fieldBoyLogin")]
    public async Task<IActionResult> FieldBoyLogin(
        [FromBody] FieldBoyLoginRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.UserIdApp) ||
            string.IsNullOrWhiteSpace(request.PasswordApp) ||
            request.BranchId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "UserIdApp, PasswordApp and BranchId are required"
            });
        }

        try
        {
            using var con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection"));

            using var cmd = new SqlCommand(
                "sp_S_FieldBoyLogin_App",
                con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("@UserIdApp", SqlDbType.NVarChar, 100)
                .Value = request.UserIdApp;

            cmd.Parameters.Add("@PasswordApp", SqlDbType.NVarChar, 100)
                .Value = request.PasswordApp;

            cmd.Parameters.Add("@BranchId", SqlDbType.Int)
                .Value = request.BranchId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid login or branch not mapped"
                });
            }

            int fieldBoyId =
                Convert.ToInt32(reader["FieldBoyId"]);

            string fieldBoyName =
                reader["FieldBoyName"]?.ToString() ?? "";

            string userIdApp =
                reader["UserIdApp"]?.ToString() ?? "";

            int loginBranchId =
                Convert.ToInt32(reader["LoginBranchId"]);

            bool isActive =
                Convert.ToBoolean(reader["IsActive"]);

            await reader.CloseAsync();

            // =========================
            // INSERT LOGIN HISTORY
            // =========================

            int loginHistoryId = 0;

            using (var loginCmd = new SqlCommand(
                "I_FieldBoyLoginHistory",
                con))
            {
                loginCmd.CommandType =
                    CommandType.StoredProcedure;

                loginCmd.Parameters.AddWithValue(
                    "@FieldBoyId",
                    fieldBoyId
                );

                loginCmd.Parameters.AddWithValue(
                "@IpAddress",
                    request.IpAddress ?? ""
                );

                loginCmd.Parameters.AddWithValue(
                    "@Browser",
                    request.Browser ?? ""
                );

                loginCmd.Parameters.AddWithValue(
                    "@Device",
                    request.Device ?? ""
                );

                loginCmd.Parameters.AddWithValue(
                    "@Os",
                    request.Os ?? ""
                );

                loginCmd.Parameters.AddWithValue(
                    "@LatitudeApp",
                    request.LatitudeApp ?? (object)DBNull.Value
                );

                loginCmd.Parameters.AddWithValue(
                    "@LongitudeApp",
                    request.LongitudeApp ?? (object)DBNull.Value
                );

                var result =
                    await loginCmd.ExecuteScalarAsync();

                loginHistoryId =
                    Convert.ToInt32(result);
            }

            // =========================
            // JWT TOKEN
            // =========================

            List<int> roles = new List<int> { 1001 };

            var jwtService = new JwtService(_config);

            var tokenResult = jwtService.GenerateToken(
                userIdApp,
                fieldBoyId,
                roles
            );

            return Ok(new
            {
                success = true,
                message = "Field boy login successful",
                token = tokenResult.token,
                expiresAtUtc = tokenResult.expiry.ToString("o"),
                expiresAtLocal = tokenResult.expiry
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd hh:mm:ss tt"),

                data = new
                {
                    fieldBoyId,
                    fieldBoyName,
                    userIdApp,
                    loginBranchId,
                    isActive,
                    loginHistoryId
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Field boy login failed",
                error = ex.Message
            });
        }
    }

    // falbo logout API

    [HttpPost("fieldBoyLogout")]
    public async Task<IActionResult> FieldBoyLogout(
    [FromQuery] int loginHistoryId
)
    {
        if (loginHistoryId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "loginHistoryId is required"
            });
        }

        try
        {
            using var con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection")
            );

            using var cmd = new SqlCommand(
                "U_FieldBoyLogoutHistory",
                con
            );

            cmd.CommandType =
                CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@LoginHistoryId",
                loginHistoryId
            );

            await con.OpenAsync();

            int rowsAffected = 0;

            using var reader =
                await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                rowsAffected =
                    Convert.ToInt32(
                        reader["RowsAffected"]
                    );
            }

            return Ok(new
            {
                success = rowsAffected > 0,
                message = rowsAffected > 0
                    ? "Field boy logout successful"
                    : "Login history not found"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Logout failed",
                error = ex.Message
            });
        }
    }

    // =========================
    // 4. Get Field Boy Login History
    // =========================

    [HttpGet("fieldBoyLoginHistory")]
    public async Task<IActionResult> GetFieldBoyLoginHistory(
        [FromQuery] int fieldBoyId
    )
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
                _config.GetConnectionString("DefaultConnection")
            );

            using var cmd = new SqlCommand(
                "S_GetFieldBoyLoginHistoryByFieldBoyId",
                con
            );

            cmd.CommandType =
                CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@FieldBoyId",
                fieldBoyId
            );

            await con.OpenAsync();

            var data = new List<Dictionary<string, object>>();

            using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row =
                    new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] =
                        reader.IsDBNull(i)
                        ? null
                        : reader.GetValue(i);
                }

                data.Add(row);
            }

            return Ok(new
            {
                success = true,
                message = "Login history fetched successfully",
                count = data.Count,
                data
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to fetch login history",
                error = ex.Message
            });
        }
    }
}

