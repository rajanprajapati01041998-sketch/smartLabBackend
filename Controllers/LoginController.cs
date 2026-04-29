using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;

[Route("api/[controller]")]
[ApiController]
public class LoginController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IPAddressService _ipService;

    public LoginController(IConfiguration config, IPAddressService ipService)
    {
        _config = config;
        _ipService = ipService;

    }

    // ✅ 1. Get Branch List
    [HttpPost("branch-list")]
    public IActionResult UserLoginBranchList([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.UserPassword))
        {
            return BadRequest("Username and Password are required");
        }

        var resultList = new List<object>();

        try
        {
            using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand("S_GetUserWiseBranchListForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@userName", SqlDbType.NVarChar).Value = request.UserName;
            cmd.Parameters.Add("@userPassword", SqlDbType.NVarChar).Value = request.UserPassword;

            con.Open();

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                resultList.Add(new
                {
                    isExist = Convert.ToInt32(reader["isExist"]),
                    branchId = reader["BranchId"] != DBNull.Value ? Convert.ToInt32(reader["BranchId"]) : (int?)null,
                    branchCode = reader["BranchCode"]?.ToString(),
                    branchName = reader["BranchName"]?.ToString()
                });
            }

            return Ok(resultList);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error occurred", error = ex.Message });
        }
    }

    // ✅ 2. Login with Branch
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.UserPassword))
        {
            return BadRequest("Invalid request");
        }

        try
        {
            using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand("sp_S_Login", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = request.UserName;
            cmd.Parameters.Add("@Password", SqlDbType.NVarChar).Value = request.UserPassword;
            cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = request.BranchId;

            con.Open();

            int id = 0;
            string userName = "";
            string name = "";
            string gender = "";
            DateTime? dob = null;
            DateTime? createdDate = null;

            List<int> roles = new List<int>();

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (id == 0)
                    {
                        id = Convert.ToInt32(reader["Id"]);
                        userName = reader["UserName"]?.ToString();
                        name = reader["Name"]?.ToString();
                        gender = reader["Gender"]?.ToString();

                        dob = reader["DOB"] != DBNull.Value
                            ? Convert.ToDateTime(reader["DOB"])
                            : null;
                    }

                    if (reader["RoleId"] != DBNull.Value)
                    {
                        roles.Add(Convert.ToInt32(reader["RoleId"]));
                    }
                }

                // ✅ IMPORTANT FIX
                reader.Close();
            }

            if (id == 0)
            {
                return Unauthorized(new { message = "Invalid username, password or branch" });
            }

            // ✅ Generate Token
            var jwtService = new JwtService(_config);
            var result = jwtService.GenerateToken(userName, id, roles);

            // ✅ Insert Login History
            int sessionId = 0;

            try
            {
                using SqlCommand loginCmd = new SqlCommand("sp_InsertUserLogin", con);
                loginCmd.CommandType = CommandType.StoredProcedure;

                string ipAddress = _ipService.GetIpAddress();

                loginCmd.Parameters.AddWithValue("@UserId", id);
                loginCmd.Parameters.AddWithValue("@IpAddress", ipAddress);
                loginCmd.Parameters.AddWithValue("@Browser", request.Browser ?? "React Native App");
                loginCmd.Parameters.AddWithValue("@Device", request.Device ?? "Unknown");
                loginCmd.Parameters.AddWithValue("@Os", request.Os ?? "Unknown");

                SqlParameter outputId = new SqlParameter("@Result", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                loginCmd.Parameters.Add(outputId);

                loginCmd.ExecuteNonQuery();

                sessionId = (int)outputId.Value; // ✅ IMPORTANT
            }
            catch (Exception ex)
            {
                Console.WriteLine("Insert Error: " + ex.Message);
            }

            return Ok(new
            {
                message = "Login successful",
                serverNowUtc = DateTime.UtcNow.ToString("o"),
                serverNowLocal = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt"),
                branchId = request.BranchId,
                sessionId,
                user = new
                {
                    id,
                    userName,
                    name,
                    gender,
                    dob,
                    createdDate,
                    displayName = $"{name} ({userName})"
                },

                roles,
                token = result.token,
                // Token expiry is generated in UTC; return both UTC and local for clarity.
                expiresAtUtc = result.expiry.ToString("o"),
                expiresAtLocal = result.expiry.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt")
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error occurred", error = ex.Message });
        }
    }

    // logout user
    [HttpPost("logout")]
    public IActionResult Logout([FromBody] LogoutRequest request)
    {
        if (request == null || request.SessionId <= 0)
        {
            return BadRequest(new { message = "Invalid session" });
        }

        try
        {
            using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand("sp_S_Logout", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@SessionId", request.SessionId);

            SqlParameter output = new SqlParameter("@Result", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };

            cmd.Parameters.Add(output);

            con.Open();
            cmd.ExecuteNonQuery();

            int result = (int)output.Value;

            return Ok(new
            {
                message = "Logout successful",
                sessionId = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Logout failed",
                error = ex.Message
            });
        }
    }


    [HttpGet("login-history/{userId}")]
    public IActionResult GetLoginHistory(int userId)
    {
        var list = new List<object>();

        try
        {
            using SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand("sp_GetLoginUserInFo", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@UserId", userId);

            con.Open();

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    ipAddress = reader["IPAddress"]?.ToString(),
                    device = reader["Device"]?.ToString(),
                    browser = reader["Browser"]?.ToString(),
                    os = reader["Os"]?.ToString(),
                    loginAt = reader["LoginAt"],
                    sessionId = reader["Id"]
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error fetching login history",
                error = ex.Message
            });
        }

        if (list.Count == 0)
        {
            return NotFound(new { message = "No login history found" });
        }

        return Ok(list);
    }
}
