using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FirebirdWeb.Pages
{
    [Authorize] // ✅ require cookie login
    public class DashboardModel : PageModel
    {
        public string UserEmail { get; private set; } = "";
        public string UserName  { get; private set; } = "";

        public IActionResult OnGet()
        {
            // ✅ 1) Must be authenticated (cookie)
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Login");

            // ✅ 2) Read from claims (cookie)
            var claimEmail =
                User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "";

            var claimName =
                User.FindFirst(ClaimTypes.Name)?.Value
                ?? (User.Identity?.Name ?? "");

            // ✅ 3) Read from session (if exists)
            var sessEmail = HttpContext.Session.GetString("UserEmail") ?? "";
            var sessName  = HttpContext.Session.GetString("UserName") ?? "";

            // ✅ 4) Decide final values (prefer session if already set, otherwise claims)
            UserEmail = !string.IsNullOrWhiteSpace(sessEmail) ? sessEmail : claimEmail;
            UserName  = !string.IsNullOrWhiteSpace(sessName)  ? sessName  : claimName;

            // ✅ 5) If session expired but cookie exists -> rebuild session
            if (string.IsNullOrWhiteSpace(sessEmail) && !string.IsNullOrWhiteSpace(UserEmail))
            {
                HttpContext.Session.SetString("UserEmail", UserEmail);
                HttpContext.Session.SetString("UserName", UserName ?? "");
            }

            // ✅ 6) Safety: still no email => force login
            if (string.IsNullOrWhiteSpace(UserEmail))
                return RedirectToPage("/Login");

            return Page();
        }
    }
}
