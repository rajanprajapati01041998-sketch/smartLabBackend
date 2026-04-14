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

        [HttpGet("GetServiceDetails")]
        public async Task<IActionResult> GetServiceDetails(
            int corporateId,
            int doctorId,
            int serviceItemId,
            int categoryId,
            int subCategoryId,
            int subSubCategoryId,
            string? previlegedCardNo = null,
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
                    cmd.Parameters.AddWithValue("@previlegedCardNo", string.IsNullOrWhiteSpace(previlegedCardNo) ? DBNull.Value : previlegedCardNo);
                    cmd.Parameters.AddWithValue("@bedTypeId", bedTypeId);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result = new ServiceAllDetailsForOPDBillingModel
                            {
                                MRP = GetDecimal(reader, "MRP"),
                                Rate = GetDecimal(reader, "Rate"),
                                RateListId = GetInt(reader, "RateListId"),
                                IsRateEditable = GetBool(reader, "IsRateEditable"),
                                ServiceName = GetString(reader, "ServiceName"),
                                SampleVolume = GetString(reader, "SampleVolume"),
                                ContainerColor = GetString(reader, "ContainerColor"),
                                Code = GetString(reader, "Code"),
                                CorporateAlias = GetString(reader, "CorporateAlias"),
                                CorporateCode = GetString(reader, "CorporateCode"),
                                ValidityDays = GetInt(reader, "ValidityDays"),
                                DiscountPer = GetDecimal(reader, "DiscountPer"),
                                DiscountReason = GetString(reader, "DiscountReason"),
                                IsNonPayable = GetInt(reader, "IsNonPayable"),
                                ServiceItemId = GetInt(reader, "ServiceItemId"),
                                CategoryId = GetInt(reader, "CategoryId"),
                                SubCategoryId = GetInt(reader, "SubCategoryId"),
                                SubSubCategoryId = GetInt(reader, "SubSubCategoryId"),
                                IsCorporateDiscount = GetInt(reader, "IsCorporateDiscount"),
                                IsPrivilegedCardDiscount = GetInt(reader, "IsPrivilegedCardDiscount"),
                                DefaultSampleTypeId = GetInt(reader, "DefaultSampleTypeId"),
                                SampleTypeIdList = GetString(reader, "SampleTypeIdList"),
                                SampleTypeList = GetString(reader, "SampleTypeList")
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

        private static bool HasColumn(IDataRecord reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string? GetString(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return null;

            object value = reader[columnName];
            return value == DBNull.Value ? null : value.ToString();
        }

        private static int GetInt(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return 0;

            object value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static decimal GetDecimal(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return 0;

            object value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static bool GetBool(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return false;

            object value = reader[columnName];
            return value != DBNull.Value && Convert.ToBoolean(value);
        }
    }
}