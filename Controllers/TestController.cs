using Microsoft.AspNetCore.Mvc;

namespace FirebirdWeb.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                message = "API is working",
                time = DateTime.Now
            });
        }
    }
}