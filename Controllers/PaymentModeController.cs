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


        [HttpGet("GetBankList")]
        public IActionResult GetBankList(string bankName = "", string filter = "")
        {
            List<BankDto> banks = new List<BankDto>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand("S_GetBankList", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@bankName", string.IsNullOrEmpty(bankName) ? "" : bankName);
                    cmd.Parameters.AddWithValue("@filter", string.IsNullOrEmpty(filter) ? "1=1" : filter);

                    con.Open();

                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        banks.Add(new BankDto
                        {
                            BankId = Convert.ToInt32(reader["BankId"]),
                            BankName = reader["BankName"].ToString(),
                            IsActive = Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                }
            }

            return Ok(banks);
        }

    }
}
