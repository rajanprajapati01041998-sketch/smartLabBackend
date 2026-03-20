using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;
using App.Common;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceAllDetailsForOPDBillingController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ServiceAllDetailsForOPDBillingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Get Service Details for OPD Billing
        /// </summary>
        [HttpGet("GetServiceDetails")]
        public async Task<IActionResult> GetServiceDetails(
            int corporateId,
            int doctorId,
            int serviceItemId,
            int categoryId,
            int subCategoryId,
            int subSubCategoryId,
            string previlegedCardNo = null,
            int bedTypeId = 0)
        {
            try
            {
                ServiceAllDetailsForOPDBillingModel result = new ServiceAllDetailsForOPDBillingModel();

                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("S_GetServiceAllDetailsForOPDBilling", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@corporateId", corporateId);
                    cmd.Parameters.AddWithValue("@doctorId", doctorId);
                    cmd.Parameters.AddWithValue("@serviceItemId", serviceItemId);
                    cmd.Parameters.AddWithValue("@categoryId", categoryId);
                    cmd.Parameters.AddWithValue("@subCategoryId", subCategoryId);
                    cmd.Parameters.AddWithValue("@subSubCategoryId", subSubCategoryId);
                    cmd.Parameters.AddWithValue("@previlegedCardNo", (object)previlegedCardNo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@bedTypeId", bedTypeId);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result = new ServiceAllDetailsForOPDBillingModel
                            {
                                MRP = Convert.ToDecimal(reader["MRP"]),
                                Rate = Convert.ToDecimal(reader["Rate"]),
                                RateListId = Convert.ToInt32(reader["RateListId"]),
                                IsRateEditable = Convert.ToBoolean(reader["IsRateEditable"]),
                                ServiceName = reader["ServiceName"]?.ToString(),
                                Code = reader["Code"]?.ToString(),
                                CorporateAlias = reader["CorporateAlias"]?.ToString(),
                                CorporateCode = reader["CorporateCode"]?.ToString(),
                                ValidityDays = Convert.ToInt32(reader["ValidityDays"]),
                                DiscountPer = Convert.ToDecimal(reader["DiscountPer"]),
                                DiscountReason = reader["DiscountReason"]?.ToString(),
                                IsNonPayable = Convert.ToInt32(reader["IsNonPayable"]),
                                ServiceItemId = Convert.ToInt32(reader["ServiceItemId"]),
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                SubCategoryId = Convert.ToInt32(reader["SubCategoryId"]),
                                SubSubCategoryId = Convert.ToInt32(reader["SubSubCategoryId"]),
                                IsCorporateDiscount = Convert.ToInt32(reader["IsCorporateDiscount"]),
                                IsPrivilegedCardDiscount = Convert.ToInt32(reader["IsPrivilegedCardDiscount"]),
                                DefaultSampleTypeId = Convert.ToInt32(reader["DefaultSampleTypeId"]),
                                SampleTypeIdList = reader["SampleTypeIdList"]?.ToString(),
                                SampleTypeList = reader["SampleTypeList"]?.ToString()
                            };
                        }
                    }
                }

                return Ok(new ApiResponse<ServiceAllDetailsForOPDBillingModel>
                {
                    Success = true,
                    Message = "Service details fetched successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error fetching service details",
                    Data = ex.Message
                });
            }
        }
    }
}