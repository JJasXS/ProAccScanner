using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FirebirdWeb.Pages
{
    public class ScannerModel : PageModel
    {
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }

        public IActionResult OnGet()
        {
            UserEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrWhiteSpace(UserEmail))
                return RedirectToPage("/Login");

            UserName = HttpContext.Session.GetString("UserName");
            return Page();
        }
    }
}
