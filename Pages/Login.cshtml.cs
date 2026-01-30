using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FirebirdWeb.Helpers;
using FirebirdSql.Data.FirebirdClient;

using Microsoft.AspNetCore.Authentication;                 // ✅ ADD
using Microsoft.AspNetCore.Authentication.Cookies;         // ✅ ADD
using System.Security.Claims;                              // ✅ ADD

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

        // ✅ Get SY_USER.NAME by email
        private string GetSyUserName(string email)
        {
            try
            {
                using var con = _db.GetConnection();

                const string sql = @"
                    SELECT FIRST 1 NAME
                    FROM SY_USER
                    WHERE UPPER(EMAIL) = UPPER(@Email)
                ";

                using var cmd = new FbCommand(sql, con);
                cmd.Parameters.AddWithValue("@Email", (email ?? "").Trim());

                return cmd.ExecuteScalar()?.ToString()?.Trim() ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB ERROR] GetSyUserName: " + ex.Message);
                return "";
            }
        }

        public JsonResult OnPostSendOTP()
        {
            Email = (Email ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Email))
                return new JsonResult(new { success = false, message = "Email required" });

            if (!EmailExistsInSyUser(Email))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            var otp = new Random().Next(100000, 999999).ToString();
            TempOTPStore.StoreOTP(Email, otp);

            Console.WriteLine($"[DEBUG] OTP for {Email}: {otp}");

            bool emailSent = _emailHelper.SendEmail(
                Email,
                "Your OTP Code",
                $"Your OTP is: <b>{otp}</b>"
            );

            if (!emailSent)
                return new JsonResult(new { success = false, message = "Failed to send OTP" });

            return new JsonResult(new { success = true, message = "OTP sent successfully" });
        }

        public async Task<JsonResult> OnPostVerifyOTPAsync()
        {
            Email = (Email ?? "").Trim();
            OTP = (OTP ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(OTP))
                return new JsonResult(new { success = false, message = "Email and OTP required" });

            if (!EmailExistsInSyUser(Email))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            bool isValid = TempOTPStore.ValidateOTP(Email, OTP);

            if (!isValid)
                return new JsonResult(new { success = false, message = "Invalid OTP" });

            // ✅ Get name
            var userName = GetSyUserName(Email);

            // ✅ Create login cookie (persistent)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Email),
                new Claim(ClaimTypes.Email, Email),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(userName) ? Email : userName),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,                 // ✅ stays after closing browser
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                }
            );

            // (Optional) keep session too if you want
            HttpContext.Session.SetString("UserEmail", Email);
            HttpContext.Session.SetString("UserName", userName);

            return new JsonResult(new
            {
                success = true,
                message = "OTP verified successfully",
                redirectUrl = Url.Page("/Dashboard", null, new { loggedIn = "true" })
            });
        }
    }
}
