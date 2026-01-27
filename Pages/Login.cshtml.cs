using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FirebirdWeb.Helpers;

namespace FirebirdWeb.Pages
{
    public class LoginModel : PageModel
    {
        private readonly EmailHelper _emailHelper;

        public LoginModel(EmailHelper emailHelper)
        {
            _emailHelper = emailHelper;
        }

        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string OTP { get; set; } = string.Empty;

        public JsonResult OnPostSendOTP()
        {
            if (string.IsNullOrEmpty(Email))
                return new JsonResult(new { success = false, message = "Email required" });

            var otp = new Random().Next(100000, 999999).ToString();

            TempOTPStore.StoreOTP(Email, otp);

            Console.WriteLine($"[DEBUG] OTP for {Email}: {otp}");

            bool emailSent = _emailHelper.SendEmail(Email, "Your OTP Code", $"Your OTP is: <b>{otp}</b>");
            if (!emailSent)
                return new JsonResult(new { success = false, message = "Failed to send OTP" });

            return new JsonResult(new { success = true, message = "OTP sent successfully" });
        }

        public JsonResult OnPostVerifyOTP()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(OTP))
                return new JsonResult(new { success = false, message = "Email and OTP required" });

            bool isValid = TempOTPStore.ValidateOTP(Email, OTP);

            if (isValid)
            {
                // Optional: remove OTP after verification
                // TempOTPStore.RemoveOTP(Email);

                return new JsonResult(new { success = true, message = "OTP verified successfully" });
            }

            return new JsonResult(new { success = false, message = "Invalid OTP" });
        }
    }
}