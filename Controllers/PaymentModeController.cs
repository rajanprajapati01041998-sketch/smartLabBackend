using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using LISD.Models;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentModeController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PaymentModeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET Payment Modes by Name
        [HttpGet("GetPaymentModeMaster")]
        public IActionResult GetPaymentModeMaster(string paymentModeName = "")
        {
            List<PaymentModeMaster> list = new List<PaymentModeMaster>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand("S_GetPaymentModeMaster", con);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@paymentModeName", paymentModeName ?? "");

                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new PaymentModeMaster
                    {
                        PaymentModeId = Convert.ToInt32(reader["PaymentModeId"]),
                        PaymentModeName = reader["PaymentModeName"].ToString(),
                        PayModeType = reader["PayModeType"].ToString(),
                        PayModeTypeId = Convert.ToInt32(reader["PayModeTypeId"]),
                        IsRefundAllowed = Convert.ToBoolean(reader["IsRefundAllowed"]),
                        IsActive = Convert.ToBoolean(reader["IsActive"])
                    });
                }
            }

            return Ok(list);
        }

        // GET Active Payment Modes
        [HttpGet("GetPaymentModes")]
        public IActionResult GetPaymentModes()
        {
            List<PaymentModeMaster> list = new List<PaymentModeMaster>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand("S_GetPaymentModes", con);
                cmd.CommandType = CommandType.StoredProcedure;

                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new PaymentModeMaster
                    {
                        PaymentModeId = Convert.ToInt32(reader["PaymentModeId"]),
                        PaymentModeName = reader["PaymentModeName"].ToString(),
                        PayModeType = reader["PayModeType"].ToString(),
                        PayModeTypeId = Convert.ToInt32(reader["PayModeTypeId"])
                    });
                }
            }

            return Ok(list);
        }
    }
}
