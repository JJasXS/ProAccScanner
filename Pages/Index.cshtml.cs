using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FirebirdWeb.Pages
{
    public class IndexModel : PageModel
    {
        public List<Dictionary<string, object>>? Agents { get; set; }

        public void OnGet()
        {
            // Nothing needed here for a static landing page
        }
    }
}