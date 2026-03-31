using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

[Route("api/[controller]")]
[ApiController]
public class BranchController : ControllerBase
{
    private readonly IConfiguration _config;

    public BranchController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("branch-user-list")]
    public async Task<IActionResult> GetBranchAndUserWiseBranchList(int branchId, int userId)
    {
        try
        {
            using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand("S_GetBranchAndUserWiseBranchList", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Parameters
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
                                row[reader.GetName(i)] = reader[i];
                            }

                            list.Add(row);
                        }

                        return Ok(list);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}