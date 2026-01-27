using System;
using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Models;

namespace FirebirdWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationController : ControllerBase
    {
        private readonly DbHelper _dbHelper;

        public RegistrationController(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpPost("register")]
        public IActionResult Register([FromForm] RegistrationRequest request)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Code))
                    return BadRequest(new { success = false, error = "Code is required" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, error = "Name is required" });

                if (string.IsNullOrWhiteSpace(request.Email))
                    return BadRequest(new { success = false, error = "Email is required" });

                if (string.IsNullOrWhiteSpace(request.Passwd))
                    return BadRequest(new { success = false, error = "Password is required" });

                if (request.Passwd.Length < 6)
                    return BadRequest(new { success = false, error = "Password must be at least 6 characters" });

                // Hash the password using BCrypt
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Passwd);

                // Get the next AUTOKEY value (max + 1)
                var maxResult = _dbHelper.ExecuteSelect("SELECT MAX(AUTOKEY) as MAX_KEY FROM SY_USER");
                int nextAutoKey = 1;
                if (maxResult.Count > 0 && maxResult[0].ContainsKey("MAX_KEY"))
                {
                    var maxValue = maxResult[0]["MAX_KEY"];
                    if (maxValue != null && int.TryParse(maxValue.ToString(), out int maxKey))
                    {
                        nextAutoKey = maxKey + 1;
                    }
                }

                // Insert into SY_USER table with calculated AUTOKEY
                string insertSql = $@"
                    INSERT INTO SY_USER (AUTOKEY, CODE, NAME, PASSWD, EMAIL, MOBILE, ISACTIVE)
                    VALUES ({nextAutoKey}, '{EscapeSQL(request.Code)}', '{EscapeSQL(request.Name)}', '{EscapeSQL(hashedPassword)}', '{EscapeSQL(request.Email)}', '{EscapeSQL(request.Mobile ?? "")}', TRUE)
                ";

                _dbHelper.ExecuteNonQuery(insertSql);

                return Ok(new
                {
                    success = true,
                    message = "Registration successful! You can now login.",
                    code = request.Code
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Registration failed",
                    details = ex.Message
                });
            }
        }

        // Helper method to escape SQL special characters
        private string EscapeSQL(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("'", "''");
        }
    }
}
