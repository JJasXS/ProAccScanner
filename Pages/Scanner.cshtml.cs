using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FirebirdWeb.Pages
{
    public class ScannerModel : PageModel
    {
        [BindProperty]
        public string? ScannedLocation { get; set; }

        [BindProperty]
        public string? ManualLocation { get; set; }

        public void OnGet()
        {
            // Show page
        }

        public IActionResult OnPost()
        {
            // Determine final location
            var finalLocation = string.IsNullOrWhiteSpace(ManualLocation) ? ScannedLocation : ManualLocation;

            // TODO: Save scanned code + finalLocation to database
            // Example: DbHelper.Execute("INSERT INTO ScanTable (Location) VALUES (@0)", finalLocation);

            // Redirect back to dashboard after save
            return RedirectToPage("/Dashboard");
        }
    }
}