using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Mvc;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DbTestController : ControllerBase
    {
        // Firebird connection string using the 4 important values
        private readonly string _connectionString = @"User=SYSDBA;Password=masterkey;Database=D:\SQLData\DB\ACC-TEST.FDB;DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;Pooling=true;MinPoolSize=1;MaxPoolSize=10;";

        [HttpGet("items")]
        public IActionResult GetAllItems()
        {
            try
            {
                var items = new List<object>();

                using (var conn = new FbConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = new FbCommand("SELECT * FROM ST_ITEM_TPL", conn))
                    {
                        cmd.CommandTimeout = 30;
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
{
    var row = new Dictionary<string, object>();
    for (int i = 0; i < reader.FieldCount; i++)
    {
        row[reader.GetName(i)] = reader.GetValue(i);
    }
    items.Add(row);
}
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    count = items.Count,
                    data = items
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