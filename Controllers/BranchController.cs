using Microsoft.AspNetCore.Mvc;
using System.Data;
using log4net;
using Microsoft.Data.SqlClient;

[Route("api/[controller]")]
[ApiController]
public class BranchController : ControllerBase
{
    private readonly IConfiguration _config;
    private static readonly ILog _log = LogManager.GetLogger(typeof(BranchController));

    public BranchController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("branch-user-list")]
    public async Task<IActionResult> GetBranchAndUserWiseBranchList(int branchId, int userId)
    {
        try
        {
            _log.Info($"GetBranchAndUserWiseBranchList API called. branchId={branchId}, userId={userId}");

            using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand("S_GetBranchAndUserWiseBranchList", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@branchId", branchId);
                    cmd.Parameters.AddWithValue("@userId", userId);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        var list = new List<Dictionary<string, object>>();

                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader[i] == DBNull.Value ? null : reader[i];
                            }

                            list.Add(row);
                        }

                        _log.Info($"GetBranchAndUserWiseBranchList success. branchId={branchId}, userId={userId}, count={list.Count}");

                        return Ok(new
                        {
                            status = true,
                            message = "Data fetched successfully",
                            data = list
                        });
                    }
                }
            }
        }
        catch (SqlException sqlEx)
        {
            _log.Error($"SQL error in GetBranchAndUserWiseBranchList. branchId={branchId}, userId={userId}", sqlEx);

            return StatusCode(500, new
            {
                status = false,
                message = "Database error occurred",
                error = sqlEx.Message
            });
        }
        catch (Exception ex)
        {
            _log.Error($"Unhandled error in GetBranchAndUserWiseBranchList. branchId={branchId}, userId={userId}", ex);

            return StatusCode(500, new
            {
                status = false,
                message = "Internal server error",
                error = ex.Message
            });
        }
    }
}