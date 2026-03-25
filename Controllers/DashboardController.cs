using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DashboardController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("states")]
        public async Task<IActionResult> GetDashboardStates(
            int branchId,
            int userId,
            int roleId = 0,
            string clientIdList = "",
            string fromDate = "",
            string toDate = "")
        {
            try
            {
                using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand("S_DashBoard_States", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@roleId", roleId);
                cmd.Parameters.AddWithValue("@clientIdList", clientIdList);
                cmd.Parameters.AddWithValue("@fromDate", fromDate);
                cmd.Parameters.AddWithValue("@toDate", toDate);

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var result = new
                    {
                        totalVisitedCount = reader["totalVisitedCount"],
                        totalCollection = reader["totalCollection"],
                        grandTotalCollection = reader["grandTotalCollection"],
                        totalHospitalCollection = reader["totalHospitalCollection"],
                        totalStoreCollection = reader["totalStoreCollection"],
                        totalSamplePending = reader["totalSamplePending"],
                        totalSampleCollected = reader["totalSampleCollected"],
                        totalResultsPending = reader["totalResultsPending"],
                        totalResultsDone = reader["totalResultsDone"],
                        todayPurchaseTotalInvoice = reader["todayPurchaseTotalInvoice"],
                        todayPurchaseTotalMRP = reader["todayPurchaseTotalMRP"],
                        totalStockOnTrade = reader["totalStockOnTrade"],
                        totalStockOnMRP = reader["totalStockOnMRP"]
                    };

                    return Ok(result);
                }

                return Ok(new { message = "No data found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}