using FirebirdWeb.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FirebirdWeb.Pages
{
    public class ActivateModel : PageModel
    {
        private readonly KeygenService _keygenService;
        private readonly ActivationState _activationState;

        public ActivateModel(KeygenService keygenService, ActivationState activationState)
        {
            _keygenService = keygenService;
            _activationState = activationState;
        }

        [BindProperty]
        public string LicenseKey { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            var activated = string.Equals(HttpContext.Session.GetString("LicenseActivated"), "true", StringComparison.OrdinalIgnoreCase)
                            || _activationState.IsActivated();
            if (activated)
                return RedirectToPage("/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LicenseKey = (LicenseKey ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(LicenseKey))
            {
                ErrorMessage = "Activation code is required.";
                return Page();
            }

            var (valid, message, _) = await _keygenService.ValidateLicenseAsync(LicenseKey);
            if (!valid)
            {
                ErrorMessage = message;
                return Page();
            }

            HttpContext.Session.SetString("LicenseActivated", "true");
            _activationState.MarkActivated();
            return RedirectToPage("/Index");
        }
    }
}
