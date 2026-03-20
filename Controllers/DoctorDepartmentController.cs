using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using LISD.Models;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorDepartmentController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DoctorDepartmentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [Route("SaveDepartment")]
        public IActionResult SaveDepartment(DepartmentRequest model)
        {
            int result = 0;

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand("IU_DoctorDepartmentMaster", con);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@DepartmentId", model.DepartmentId);
                cmd.Parameters.AddWithValue("@Department", model.Department);
                cmd.Parameters.AddWithValue("@HospId", model.HospId);
                cmd.Parameters.AddWithValue("@UserId", model.UserId);
                cmd.Parameters.AddWithValue("@IpAddress", model.IpAddress);

                SqlParameter output = new SqlParameter("@Result", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                cmd.Parameters.Add(output);

                con.Open();
                cmd.ExecuteNonQuery();

                result = Convert.ToInt32(output.Value);
            }

            if (result == -1)
            {
                return Ok("Department already exists");
            }

            return Ok(new
            {
                DepartmentId = result,
                Message = "Department Saved Successfully"
            });
        }
    }
}
