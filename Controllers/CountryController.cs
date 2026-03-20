using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using LISD.Models;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountryController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public CountryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetCountries")]
        public IActionResult GetCountries(string? filter = null)
        {
            List<CountryMaster> countries = new List<CountryMaster>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand("S_GetCountryMaster", con);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@filter", filter is null ? DBNull.Value : filter);

                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    countries.Add(new CountryMaster
                    {
                        CountryId = Convert.ToInt32(reader["CountryId"]),
                        CountryName = reader["CountryName"].ToString(),
                        Currency = reader["Currency"].ToString(),
                        ConversionFactor = Convert.ToDecimal(reader["ConversionFactor"]),
                        IsActive = Convert.ToBoolean(reader["IsActive"])
                    });
                }
            }

            return Ok(countries);
        }
    }
}
