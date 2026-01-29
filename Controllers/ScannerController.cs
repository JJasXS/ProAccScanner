using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Helpers;
using FirebirdWeb.Models;
using System;
using System.Collections.Generic;

namespace FirebirdWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScannerController : ControllerBase
    {
        private readonly DbHelper _dbHelper;

        public ScannerController(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // ----------------------------
        // 1️⃣ Validate scanned code
        // Flow:
        // scannedCode -> ST_ITEM_TPLDTL.CODE
        // ST_ITEM_TPLDTL.LOCATION (location code)
        // -> ST_LOCATION.CODE -> return ST_LOCATION.DESCRIPTION (full name)
        // ----------------------------
        [HttpPost("validate")]
        public IActionResult Validate([FromBody] ScannerRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new
                {
                    success = false,
                    cause = "EMPTY_CODE",
                    message = "Scanned code is missing."
                });
            }

            string code = request.Code.Trim().ToUpper();

            try
            {
                string safeCode = EscapeSQL(code);

                // ✅ NEW: join to ST_LOCATION to show DESCRIPTION instead of code
                // - D.LOCATION == L.CODE (as you explained)
                // - If location is missing / not found, return empty string (UI handles it)
                string sql = $@"
SELECT FIRST 1
    TRIM(COALESCE(L.DESCRIPTION, '')) AS LOCATION
FROM ST_ITEM_TPLDTL D
JOIN ST_ITEM_TPL T
    ON TRIM(T.CODE) = TRIM(D.CODE)
LEFT JOIN ST_LOCATION L
    ON UPPER(TRIM(L.CODE)) = UPPER(TRIM(D.LOCATION))
WHERE UPPER(TRIM(D.CODE)) = '{safeCode}'
";

                List<Dictionary<string, object>> results = _dbHelper.ExecuteSelect(sql);

                if (results.Count > 0)
                {
                    string location = "";

                    if (results[0].ContainsKey("LOCATION") && results[0]["LOCATION"] != null)
                    {
                        location = results[0]["LOCATION"].ToString()?.Trim() ?? "";
                    }

                    return Ok(new
                    {
                        success = true,
                        exists = true,
                        location
                    });
                }

                return Ok(new
                {
                    success = true,
                    exists = false,
                    message = "Code not found in database."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    cause = "DB_ERROR",
                    message = "Database query failed.",
                    detail = ex.Message
                });
            }
        }

        // ----------------------------
        // 2️⃣ Get all existing locations (manual selection dropdown)
        // ----------------------------
        [HttpGet("locations")]
        public IActionResult GetLocations()
        {
            try
            {
                string sql = @"
SELECT DISTINCT TRIM(DESCRIPTION) AS DESCRIPTION
FROM ST_LOCATION
WHERE DESCRIPTION IS NOT NULL
  AND TRIM(DESCRIPTION) <> ''
ORDER BY DESCRIPTION
";

                List<Dictionary<string, object>> results = _dbHelper.ExecuteSelect(sql);

                var locations = new List<string>();
                foreach (var row in results)
                {
                    if (row.ContainsKey("DESCRIPTION") && row["DESCRIPTION"] != null)
                    {
                        string desc = row["DESCRIPTION"].ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(desc))
                            locations.Add(desc);
                    }
                }

                return Ok(new { success = true, locations });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private string EscapeSQL(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("'", "''");
        }
    }
}
