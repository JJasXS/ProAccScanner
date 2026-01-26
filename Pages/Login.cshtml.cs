using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FirebirdWeb.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string? Username { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        public void OnGet()
        {
            // Optional pre-login logic
        }

        public IActionResult OnPost()
        {
            // Redirect to Dashboard regardless of input
            return RedirectToPage("/Dashboard");
            //s
        }
    }
}