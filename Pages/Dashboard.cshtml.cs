using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;

namespace FirebirdWeb.Pages
{
    public class DashboardModel : PageModel
    {
        public void OnGet()
        {
            // Simple protection: require OTP "login" before accessing dashboard
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
            {
                Response.Redirect("/Login");
                return;
            }

            // Any data for the dashboard can be loaded here, using 'email' if needed
        }
    }
}