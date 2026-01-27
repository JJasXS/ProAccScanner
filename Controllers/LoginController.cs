using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Helpers;

namespace FirebirdWeb.Controllers
{
    [Route("api/login")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly EmailHelper _emailHelper;

        public LoginController(EmailHelper emailHelper)
        {
            _emailHelper = emailHelper;
        }

        [HttpPost("sendotp")]
        public IActionResult SendOTP([FromForm] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { success = false, error = "Email is required" });

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();

            // Store OTP temporarily (or you can use DbHelper to save in DB)
            TempOTPStore.StoreOTP(email, otp);

            // Send email
            try
            {
                _emailHelper.SendEmail(email, "Your OTP Code", $"Your OTP code is: {otp}");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = "Failed to send OTP: " + ex.Message });
            }
        }

        [HttpPost("verifyotp")]
        public IActionResult VerifyOTP([FromForm] string email, [FromForm] string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return BadRequest(new { success = false, error = "Email and OTP required" });

            bool valid = TempOTPStore.ValidateOTP(email, otp);

            if (valid)
            {
                var user = new { Email = email };
                return Ok(new { success = true, user });
            }
            else
            {
                return Ok(new { success = false, error = "Invalid or expired OTP" });
            }
        }
    }
}