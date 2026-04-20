using FirebirdWeb.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace FirebirdWeb.Controllers
{
    [Route("api/keygen")]
    [ApiController]
    public class KeygenController : ControllerBase
    {
        private readonly KeygenService _keygen;

        public KeygenController(KeygenService keygen)
        {
            _keygen = keygen;
        }

        // POST /api/keygen/validate
        // Body (form): licenseKey=YOUR-KEY-HERE
        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromForm] string licenseKey)
        {
            var (valid, message, licenseId) = await _keygen.ValidateLicenseAsync(licenseKey);

            return Ok(new
            {
                success = valid,
                message,
                licenseId
            });
        }

        // GET /api/keygen/validate?licenseKey=YOUR-KEY-HERE  (handy for quick browser test)
        [HttpGet("validate")]
        public async Task<IActionResult> ValidateGet([FromQuery] string licenseKey)
        {
            var (valid, message, licenseId) = await _keygen.ValidateLicenseAsync(licenseKey);

            return Ok(new
            {
                success = valid,
                message,
                licenseId
            });
        }
    }
}
