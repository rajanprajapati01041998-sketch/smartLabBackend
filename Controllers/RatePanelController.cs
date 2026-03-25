using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

[Route("api/[controller]")]
[ApiController]
public class RateController : ControllerBase
{
    private readonly IConfiguration _config;

    public RateController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("rate-list/{branchId}")]
    public async Task<IActionResult> GetDefaultRateList(int branchId)
    {
        try
        {
            using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand("S_getDefaultRateListByBranchId2", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BranchId", branchId);

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

                        return Ok(list); // ✅ works perfectly
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