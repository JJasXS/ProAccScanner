using System;
using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Models;
using FirebirdWeb.Helpers;

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

                // Check if CODE already exists
                var codeCheck = _dbHelper.ExecuteSelect($"SELECT CODE FROM SY_USER WHERE CODE = '{EscapeSQL(request.Code)}'");
                if (codeCheck.Count > 0)
                    return BadRequest(new { success = false, error = "User Code already exists. Please choose a different code." });

                // Check if EMAIL already exists
                var emailCheck = _dbHelper.ExecuteSelect($"SELECT EMAIL FROM SY_USER WHERE EMAIL = '{EscapeSQL(request.Email)}'");
                if (emailCheck.Count > 0)
                    return BadRequest(new { success = false, error = "Email already registered. Please use a different email or login." });

                // Hash the password using BCrypt
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Passwd);

                // Get the next AUTOKEY value (max + 1) - AUTOKEY is required by the table
                var maxResult = _dbHelper.ExecuteSelect("SELECT MAX(AUTOKEY) as MAX_KEY FROM SY_USER");
                int nextAutoKey = 1;
                if (maxResult.Count > 0 && maxResult[0].ContainsKey("MAX_KEY"))
                {
                    var maxValue = maxResult[0]["MAX_KEY"];
                    if (maxValue != null && maxValue != DBNull.Value && int.TryParse(maxValue.ToString(), out int maxKey))
                    {
                        nextAutoKey = maxKey + 1;
                    }
                }

                // Insert into SY_USER table with AUTOKEY and columns: CODE, NAME, PASSWD, MOBILE, EMAIL
                string insertSql = $@"
                    INSERT INTO SY_USER (AUTOKEY, CODE, NAME, PASSWD, MOBILE, EMAIL)
                    VALUES ({nextAutoKey}, '{EscapeSQL(request.Code)}', '{EscapeSQL(request.Name)}', '{EscapeSQL(hashedPassword)}', '{EscapeSQL(request.Mobile ?? "")}', '{EscapeSQL(request.Email)}')
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
                // Log the full error for debugging
                Console.WriteLine($"[REGISTRATION ERROR] {ex.Message}");
                Console.WriteLine($"[REGISTRATION ERROR] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[REGISTRATION ERROR] InnerException: {ex.InnerException.Message}");
                }

                return BadRequest(new
                {
                    success = false,
                    error = "Registration failed",
                    details = ex.Message,
                    innerException = ex.InnerException?.Message
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
