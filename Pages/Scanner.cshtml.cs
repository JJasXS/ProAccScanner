using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FirebirdWeb.Pages
{
    public class ScannerModel : PageModel
    {
        // Runs when the page loads
        public void OnGet()
        {
        }

        // Handles AJAX POST from JS when a barcode is scanned
        [IgnoreAntiforgeryToken] // allows JS POST without anti-forgery token
        public IActionResult OnPostLogScan([FromBody] ScanRequest request)
        {
            // Log scanned barcode in DEBUG format
            Console.WriteLine($"[DEBUG] Scanned barcode: {request.Code}");

            // Can later add DB lookup here
            return new JsonResult(new { success = true });
        }

        // Class to parse JSON sent from JS fetch
        public class ScanRequest
        {
            public string Code { get; set; } = "";
        }
    }
}