using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FirebirdWeb.Pages
{
    public class RegisterModel : PageModel
    {
        // Bind properties from the form
        [BindProperty]
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            // Just display the form
            return Page();
        }

        public IActionResult OnPost()
        {
            // Server-side validation
            if (!ModelState.IsValid)
            {
                // If any validation fails, stay on the page and show errors
                return Page();
            }

            // TODO: Insert into database here
            // Example:
            // DbHelper db = new DbHelper();
            // db.ExecuteNonQuery($"INSERT INTO users (username,password,email) VALUES ('{Username}','{Password}','{Email}')");

            // After successful registration, redirect to login page
            return RedirectToPage("/Login");
        }
    }
}