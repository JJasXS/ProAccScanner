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
        // Flow: scannedCode -> ST_ITEM_TPL.CODE -> ST_ITEM_TPLDTL.CODE -> get LOCATION
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

            // Keep your existing normalization
            string code = request.Code.Trim().ToUpper();

            try
            {
                // ✅ FIXED:
                // - Filter only once using D.CODE (most common place for detail rows)
                // - Join on TRIM(T.CODE)=TRIM(D.CODE) (no redundant UPPER comparison on join)
                // - Use TRIM() for Firebird
                // - Avoid returning "No location assigned" string; return "" so UI handles it
                string safeCode = EscapeSQL(code);

                string sql = $@"
SELECT FIRST 1
    TRIM(D.LOCATION) AS LOCATION
FROM ST_ITEM_TPLDTL D
JOIN ST_ITEM_TPL T
    ON TRIM(T.CODE) = TRIM(D.CODE)
WHERE UPPER(TRIM(D.CODE)) = '{safeCode}'
";

                List<Dictionary<string, object>> results = _dbHelper.ExecuteSelect(sql);

                if (results.Count > 0)
                {
                    string location = "";

                    if (results[0].ContainsKey("LOCATION") && results[0]["LOCATION"] != null)
                    {
                        location = results[0]["LOCATION"].ToString();
                        location = location == null ? "" : location.Trim();
                    }

                    // If location is null/empty, return empty string (frontend already handles this)
                    return Ok(new
                    {
                        success = true,
                        exists = true,
                        location = location ?? ""
                    });
                }

                // Code not found
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
        // 2️⃣ Get all existing locations (for manual selection dropdown)
        // Frontend calls: GET /api/scanner/locations
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

        // ----------------------------
        // Helper: escape single quotes to prevent SQL errors
        // ----------------------------
        private string EscapeSQL(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("'", "''");
        }
    }
}
