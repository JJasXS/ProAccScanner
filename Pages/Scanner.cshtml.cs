using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FirebirdWeb.Pages
{
    [Authorize] // ✅ require cookie login
    public class ScannerModel : PageModel
    {
        public string UserEmail { get; private set; } = "";
        public string UserName  { get; private set; } = "";

        public IActionResult OnGet()
        {
            // 1) Must be authenticated (cookie only)
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Login");

            // 2) Always use claims for login info
            UserEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "";
            UserName = User.FindFirst(ClaimTypes.Name)?.Value
                ?? (User.Identity?.Name ?? "");

            // 3) Optionally rebuild session for convenience (not required for login)
            var sessEmail = HttpContext.Session.GetString("UserEmail") ?? "";
            var sessName  = HttpContext.Session.GetString("UserName") ?? "";
            if (string.IsNullOrWhiteSpace(sessEmail) && !string.IsNullOrWhiteSpace(UserEmail))
            {
                HttpContext.Session.SetString("UserEmail", UserEmail);
                HttpContext.Session.SetString("UserName", UserName ?? "");
            }

            // 4) If not authenticated, force login (should never hit here)
            if (string.IsNullOrWhiteSpace(UserEmail))
                return RedirectToPage("/Login");

            return Page();
        }
    }
}
