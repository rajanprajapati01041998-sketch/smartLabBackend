using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using App.Models;
using App.Common;

namespace App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvestigationController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public InvestigationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Search Investigation for Consultation
        /// </summary>
        [HttpGet("SearchInvestigation")]
        public async Task<IActionResult> SearchInvestigation(string searchText, int doctorId)
        {
            try
            {
                var list = new List<InvestigationSearchModel>();

                // 🔹 Step 1: Call Stored Procedure
                using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("S_SearchInvestigationForConsultation", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@searchText", searchText ?? "");
                    cmd.Parameters.AddWithValue("@doctorId", doctorId);

                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new InvestigationSearchModel
                            {
                                ItemId = Convert.ToInt32(reader["ItemId"]),
                                Name = reader["Name"]?.ToString(),
                                IsExternal = Convert.ToBoolean(reader["IsExternal"]),

                                // default null (will fill later)
                                CategoryId = null,
                                SubCategoryId = null,
                                SubSubCategoryId = null
                            });
                        }
                    }
                }

                // 🔹 Step 2: Get internal item IDs
                var internalIds = list
                    .Where(x => !x.IsExternal)
                    .Select(x => x.ItemId)
                    .ToList();

                // 🔹 Step 3: Fetch Category Data
                if (internalIds.Any())
                {
                    using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        var query = $@"
                    SELECT ServiceItemId, CategoryId, SubCategoryId, SubSubCategoryId
                    FROM ServiceItemMaster
                    WHERE ServiceItemId IN ({string.Join(",", internalIds)})
                ";

                        using (SqlCommand cmd = new SqlCommand(query, con))
                        {
                            await con.OpenAsync();

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                var categoryMap = new Dictionary<int, (int?, int?, int?)>();

                                while (await reader.ReadAsync())
                                {
                                    int id = Convert.ToInt32(reader["ServiceItemId"]);

                                    categoryMap[id] = (
                                        reader["CategoryId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CategoryId"]),
                                        reader["SubCategoryId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SubCategoryId"]),
                                        reader["SubSubCategoryId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SubSubCategoryId"])
                                    );
                                }

                                // 🔹 Step 4: Map data back to list
                                foreach (var item in list)
                                {
                                    if (!item.IsExternal && categoryMap.ContainsKey(item.ItemId))
                                    {
                                        var cat = categoryMap[item.ItemId];

                                        item.CategoryId = cat.Item1;
                                        item.SubCategoryId = cat.Item2;
                                        item.SubSubCategoryId = cat.Item3;
                                    }
                                }
                            }
                        }
                    }
                }

                // 🔹 Final Response
                return Ok(new ApiResponse<List<InvestigationSearchModel>>
                {
                    Success = true,
                    Message = "Investigation list fetched successfully",
                    Data = list
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Error fetching investigation list",
                    Data = ex.Message
                });
            }
        }
    }
}