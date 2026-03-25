using App.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

[Route("api/[controller]")]
[ApiController]
public class HelpDeskController : ControllerBase
{
    private readonly IConfiguration _config;

    public HelpDeskController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("help_desk")]
    public async Task<IActionResult> GetInvestigation([FromBody] HelpDeskRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connString))
            using (SqlCommand cmd = new SqlCommand("S_GetPatientInvestigationForLabHelpDesk", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // ✅ Directly use DTO properties
                cmd.Parameters.AddWithValue("@branchId", request.branchId ?? "1");
                cmd.Parameters.AddWithValue("@typeId", request.typeId ?? "0");
                cmd.Parameters.AddWithValue("@uhid", string.IsNullOrEmpty(request.uhid) ? DBNull.Value : request.uhid);
                cmd.Parameters.AddWithValue("@ipdNo", string.IsNullOrEmpty(request.ipdNo) ? DBNull.Value : request.ipdNo);
                cmd.Parameters.AddWithValue("@labNo", string.IsNullOrEmpty(request.labNo) ? DBNull.Value : request.labNo);
                cmd.Parameters.AddWithValue("@fromDate", string.IsNullOrEmpty(request.fromDate) ? DBNull.Value : request.fromDate);
                cmd.Parameters.AddWithValue("@toDate", string.IsNullOrEmpty(request.toDate) ? DBNull.Value : request.toDate);
                cmd.Parameters.AddWithValue("@barCode", string.IsNullOrEmpty(request.barCode) ? DBNull.Value : request.barCode);
                cmd.Parameters.AddWithValue("@subCategoryId", string.IsNullOrEmpty(request.subCategoryId) ? DBNull.Value : request.subCategoryId);
                cmd.Parameters.AddWithValue("@subSubCategoryId", string.IsNullOrEmpty(request.subSubCategoryId) ? DBNull.Value : request.subSubCategoryId);
                cmd.Parameters.AddWithValue("@investigationName", string.IsNullOrEmpty(request.investigationName) ? DBNull.Value : request.investigationName);
                cmd.Parameters.AddWithValue("@patientName", string.IsNullOrEmpty(request.patientName) ? DBNull.Value : request.patientName);
                cmd.Parameters.AddWithValue("@branchIdList", string.IsNullOrEmpty(request.branchIdList) ? "1" : request.branchIdList);
                cmd.Parameters.AddWithValue("@corporateId", string.IsNullOrEmpty(request.corporateId) ? DBNull.Value : request.corporateId);
                cmd.Parameters.AddWithValue("@roleId", string.IsNullOrEmpty(request.roleId) ? "0" : request.roleId);
                cmd.Parameters.AddWithValue("@filter", string.IsNullOrEmpty(request.filter) ? DBNull.Value : request.filter);

                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<Dictionary<string, object>>();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader[i];
                    }

                    result.Add(row);
                }

                return Ok(new
                {
                    success = true,
                    count = result.Count,
                    data = result
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}