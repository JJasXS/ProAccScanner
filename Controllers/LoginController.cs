using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace FirebirdWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly DbHelper _dbHelper;

        public LoginController(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromForm] string code, [FromForm] string passwd)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(code))
                    return BadRequest(new { success = false, error = "User Code is required" });

                if (string.IsNullOrWhiteSpace(passwd))
                    return BadRequest(new { success = false, error = "Password is required" });

                // Query the database for the user by CODE
                string query = $"SELECT AUTOKEY, CODE, NAME, EMAIL, PASSWD, ISACTIVE FROM SY_USER WHERE CODE = '{EscapeSQL(code)}'";
                var results = _dbHelper.ExecuteSelect(query);

                if (results.Count == 0)
                    return Unauthorized(new { success = false, error = "Invalid Code or Password" });

                var user = results[0];

                // Check if user is active
                var isActive = user.ContainsKey("ISACTIVE") ? user["ISACTIVE"] : false;
                if (!Convert.ToBoolean(isActive))
                    return Unauthorized(new { success = false, error = "User account is inactive" });

                // Get stored password hash
                string storedHash = user["PASSWD"]?.ToString() ?? "";

                // Verify password using BCrypt
                bool isValidPassword = BCrypt.Net.BCrypt.Verify(passwd, storedHash);

                if (!isValidPassword)
                    return Unauthorized(new { success = false, error = "Invalid Code or Password" });

                // Login successful - return user info (without password)
                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    user = new
                    {
                        autoKey = user["AUTOKEY"],
                        code = user["CODE"],
                        name = user["NAME"],
                        email = user["EMAIL"]
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Login failed",
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
