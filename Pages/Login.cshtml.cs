using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FirebirdWeb.Helpers;
using FirebirdSql.Data.FirebirdClient;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

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

        // ✅ Check user exists + active (IsActive)
        // NOTE: This supports ISACTIVE values like 1/0 OR Y/N OR TRUE/FALSE.
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

            // ✅ 1) validate exists + active
            var (exists, active) = GetUserExistsAndActive(Email);

            if (!exists)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            if (!active)
            {
                return new JsonResult(new
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

            // ✅ 1) validate exists + active again (important)
            var (exists, active) = GetUserExistsAndActive(Email);

            if (!exists)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Email not registered. Please create an account first."
                });
            }

            if (!active)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "This user is inactive. Please contact admin."
                });
            }

            // ✅ 2) validate OTP
            bool isValid = TempOTPStore.ValidateOTP(Email, OTP);

            if (!isValid)
                return new JsonResult(new { success = false, message = "Invalid OTP" });

            // ✅ 3) Get name
            var userName = GetSyUserName(Email);

            // ✅ 4) Create login cookie (persistent)
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
                    IsPersistent = true, // ✅ stays after closing browser
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                }
            );

            // ✅ Optional: keep session too
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
