using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Helpers;

namespace FirebirdWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DbTestController : ControllerBase
    {
        private readonly DbHelper _dbHelper;

        public DbTestController(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            try
            {
                var users = _dbHelper.ExecuteSelect("SELECT CODE, PASSWD FROM SY_USER");

                return Ok(new
                {
                    success = true,
                    count = users.Count,
                    data = users
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }
    }
}