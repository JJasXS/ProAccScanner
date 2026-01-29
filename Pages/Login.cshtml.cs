using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FirebirdWeb.Helpers;
using FirebirdSql.Data.FirebirdClient;

namespace FirebirdWeb.Pages
{
    [IgnoreAntiforgeryToken] // Allow AJAX POSTs without antiforgery token
    public class LoginModel : PageModel
    {
        private readonly EmailHelper _emailHelper;
        private readonly DbHelper _db;

        public LoginModel(EmailHelper emailHelper, DbHelper db)
        {
            _emailHelper = emailHelper;
            _db = db;
        }

        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string OTP { get; set; } = string.Empty;

        // ✅ Firebird: check SY_USER for email
        private bool EmailExistsInSyUser(string email)
        {
            try
            {
                using var con = _db.GetConnection();

                const string sql = @"
                    SELECT FIRST 1 EMAIL
                    FROM SY_USER
                    WHERE UPPER(EMAIL) = UPPER(@Email)
                ";

                using var cmd = new FbCommand(sql, con);
                cmd.Parameters.AddWithValue("@Email", (email ?? "").Trim());

                return cmd.ExecuteScalar() != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB ERROR] EmailExistsInSyUser: " + ex.Message);
                return false;
            }
        }

        public JsonResult OnPostSendOTP()
        {
            Email = (Email ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Email))
                return new JsonResult(new { success = false, message = "Email required" });

            // ✅ 1) Validate email exists in DB
            if (!EmailExistsInSyUser(Email))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            // ✅ 2) Generate + store OTP
            var otp = new Random().Next(100000, 999999).ToString();
            TempOTPStore.StoreOTP(Email, otp);

            Console.WriteLine($"[DEBUG] OTP for {Email}: {otp}");

            // ✅ 3) Send OTP email
            bool emailSent = _emailHelper.SendEmail(
                Email,
                "Your OTP Code",
                $"Your OTP is: <b>{otp}</b>"
            );

            if (!emailSent)
                return new JsonResult(new { success = false, message = "Failed to send OTP" });

            return new JsonResult(new { success = true, message = "OTP sent successfully" });
        }

        public JsonResult OnPostVerifyOTP()
        {
            Email = (Email ?? "").Trim();
            OTP = (OTP ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(OTP))
                return new JsonResult(new { success = false, message = "Email and OTP required" });

            // (Optional but good) ✅ ensure email still exists
            if (!EmailExistsInSyUser(Email))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            bool isValid = TempOTPStore.ValidateOTP(Email, OTP);

            if (isValid)
            {
                HttpContext.Session.SetString("UserEmail", Email);

                return new JsonResult(new
                {
                    success = true,
                    message = "OTP verified successfully",
                    redirectUrl = Url.Page("/Dashboard")
                });
            }

            return new JsonResult(new { success = false, message = "Invalid OTP" });
        }
    }
}
