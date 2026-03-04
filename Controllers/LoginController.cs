using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Helpers;
using FirebirdSql.Data.FirebirdClient;

namespace FirebirdWeb.Controllers
{
    [Route("api/login")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly EmailHelper _emailHelper;
        private readonly DbHelper _db;

        public LoginController(EmailHelper emailHelper, DbHelper db)
        {
            _emailHelper = emailHelper;
            _db = db;
        }

        // ✅ Check user exists + active (IsActive)
        private (bool exists, bool active) GetUserExistsAndActive(string email)
        {
            try
            {
                using var con = _db.GetConnection();

                const string sql = @"
                    SELECT FIRST 1 EMAIL, ISACTIVE
                    FROM SY_USER
                    WHERE UPPER(EMAIL) = UPPER(@Email)
                ";

                using var cmd = new FbCommand(sql, con);
                cmd.Parameters.AddWithValue("@Email", (email ?? "").Trim());

                using var r = cmd.ExecuteReader();
                if (!r.Read()) return (false, false);

                var isActiveObj = r["ISACTIVE"];
                var isActiveStr = isActiveObj?.ToString()?.Trim() ?? "";

                bool active =
                    isActiveStr == "1" ||
                    isActiveStr.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                    isActiveStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase);

                return (true, active);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB ERROR] GetUserExistsAndActive: " + ex.Message);
                return (false, false);
            }
        }

        // POST: /api/login/sendotp
        [HttpPost("sendotp")]
        public IActionResult SendOTP([FromForm] string Email)
        {
            Email = (Email ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Email))
                return BadRequest(new { success = false, message = "Email is required" });

            // ✅ 1) Validate exists + active
            var (exists, active) = GetUserExistsAndActive(Email);

            if (!exists)
            {
                return Ok(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            if (!active)
            {
                return Ok(new
                {
                    success = false,
                    message = "This user is inactive. Please contact admin."
                });
            }

            // ✅ 2) Generate + store OTP
            var otp = new Random().Next(100000, 999999).ToString();
            TempOTPStore.StoreOTP(Email, otp);

            Console.WriteLine($"[DEBUG] OTP for {Email}: {otp}");

            // ✅ 3) Send OTP email
            try
            {
                bool sent = _emailHelper.SendEmail(
                    Email,
                    "Your OTP Code",
                    $"Your OTP code is: <b>{otp}</b>"
                );

                if (!sent)
                    return Ok(new { success = false, message = "Failed to send OTP email." });

                return Ok(new { success = true, message = "OTP sent successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EMAIL ERROR] SendEmail: " + ex.Message);
                return Ok(new { success = false, message = "Failed to send OTP." });
            }
        }

        // POST: /api/login/verifyotp
        [HttpPost("verifyotp")]
        public IActionResult VerifyOTP([FromForm] string Email, [FromForm] string OTP)
        {
            Email = (Email ?? "").Trim();
            OTP = (OTP ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(OTP))
                return BadRequest(new { success = false, message = "Email and OTP required" });

            // ✅ 1) Validate exists + active again
            var (exists, active) = GetUserExistsAndActive(Email);

            if (!exists)
            {
                return Ok(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            if (!active)
            {
                return Ok(new
                {
                    success = false,
                    message = "This user is inactive. Please contact admin."
                });
            }

            // ✅ 2) Validate OTP
            bool valid = TempOTPStore.ValidateOTP(Email, OTP);

            if (!valid)
                return Ok(new { success = false, message = "Invalid or expired OTP" });

            HttpContext.Session.SetString("UserEmail", Email);

            return Ok(new
            {
                success = true,
                message = "OTP verified successfully",
                redirectUrl = "/Dashboard"
            });
        }
    }
}
