using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using LISD.Models;
using log4net;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountryController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // ✅ log4net instance
        private static readonly ILog _log = LogManager.GetLogger(typeof(CountryController));

        public CountryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetCountries")]
        public IActionResult GetCountries(string? filter = null)
        {
            try
            {
                _log.Info($"GetCountries API called. filter={filter}");

                List<CountryMaster> countries = new List<CountryMaster>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand("S_GetCountryMaster", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@filter", filter is null ? DBNull.Value : filter);

                        con.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                countries.Add(new CountryMaster
                                {
                                    CountryId = Convert.ToInt32(reader["CountryId"]),
                                    CountryName = reader["CountryName"]?.ToString(),
                                    Currency = reader["Currency"]?.ToString(),
                                    ConversionFactor = Convert.ToDecimal(reader["ConversionFactor"]),
                                    IsActive = Convert.ToBoolean(reader["IsActive"])
                                });
                            }
                        }
                    }
                }

                _log.Info($"GetCountries success. count={countries.Count}");

                return Ok(new
                {
                    status = true,
                    message = "Data fetched successfully",
                    data = countries
                });
            }
            catch (SqlException ex)
            {
                _log.Error($"SQL error in GetCountries API. filter={filter}", ex);

                return StatusCode(500, new
                {
                    status = false,
                    message = "Database error occurred",
                    data = (object?)null
                });
            }
            catch (Exception ex)
            {
                _log.Error($"Unhandled error in GetCountries API. filter={filter}", ex);

                return StatusCode(500, new
                {
                    status = false,
                    message = "Internal server error",
                    data = (object?)null
                });
            }
        }



        // get full locatio by pincode 
        [HttpGet("GetFullLocationByPincode")]
        public IActionResult GetFullLocationByPincode(int pincode)
        {
            try
            {
                _log.Info($"GetFullLocationByPincode API called. pincode={pincode}");

                var locations = new List<object>();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand("S_GetFullLocationByPincode", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Pincode", pincode);

                        con.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                locations.Add(new
                                {
                                    CityName = reader["CityName"]?.ToString(),
                                    Pincode = reader["Pincode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Pincode"]),
                                    DistrictName = reader["DistrictName"]?.ToString(),
                                    StateName = reader["StateName"]?.ToString(),
                                    CityCode = reader["CityCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CityCode"]),
                                    DistrictCode = reader["DistrictCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DistrictCode"]),
                                    StateCode = reader["StateCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["StateCode"]),
                                    ID = reader["ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID"]),
                                    SSSS = reader["SSSS"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SSSS"])
                                });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    status = true,
                    message = locations.Count > 0 ? "Data fetched successfully" : "No location found",
                    count = locations.Count,
                    data = locations
                });
            }
            catch (SqlException ex)
            {
                _log.Error($"SQL error in GetFullLocationByPincode API. pincode={pincode}", ex);

                return StatusCode(500, new
                {
                    status = false,
                    message = "Database error occurred",
                    data = (object?)null
                });
            }
            catch (Exception ex)
            {
                _log.Error($"Unhandled error in GetFullLocationByPincode API. pincode={pincode}", ex);

                return StatusCode(500, new
                {
                    status = false,
                    message = "Internal server error",
                    data = (object?)null
                });
            }
        }
    }
}