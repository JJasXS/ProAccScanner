using Microsoft.AspNetCore.Mvc;
using FirebirdWeb.Helpers;
using Microsoft.AspNetCore.Http;
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
        // 1) Validate scanned code -> return LOCATION DESCRIPTION (human readable)
        // NOTE: If multiple rows exist for same CODE, returns latest by DTLKEY DESC.
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

                string sql = $@"
SELECT FIRST 1
    TRIM(COALESCE(L.DESCRIPTION, '')) AS LOCATION
FROM ST_ITEM_TPLDTL D
LEFT JOIN ST_LOCATION L
    ON UPPER(TRIM(L.CODE)) = UPPER(TRIM(D.LOCATION))
WHERE UPPER(TRIM(D.CODE)) = '{safeCode}'
ORDER BY D.DTLKEY DESC
";

                List<Dictionary<string, object>> results = _dbHelper.ExecuteSelect(sql);

                if (results != null && results.Count > 0)
                {
                    string location = "";

                    if (results[0].ContainsKey("LOCATION") && results[0]["LOCATION"] != null)
                        location = results[0]["LOCATION"].ToString()?.Trim() ?? "";

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
        // 2) Get all locations (dropdown) as DESCRIPTION (human readable)
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
                if (results != null)
                {
                    foreach (var row in results)
                    {
                        if (row.ContainsKey("DESCRIPTION") && row["DESCRIPTION"] != null)
                        {
                            string desc = row["DESCRIPTION"].ToString()?.Trim() ?? "";
                            if (!string.IsNullOrWhiteSpace(desc))
                                locations.Add(desc);
                        }
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
        // 3) INSERT NEW record into ST_ITEM_TPLDTL
        // ✅ ITEMCODE = CODE
        // ✅ UDF_DATETIME = current system datetime (string)
        // ✅ UDF_USER = session user (SY_USER.NAME)
        // ----------------------------
        [HttpPost("insert-detail")]
        public IActionResult InsertDetail([FromBody] InsertDetailRequest request)
        {
            // ✅ Require login session
            var sessionEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrWhiteSpace(sessionEmail))
                return Unauthorized(new { success = false, message = "Not logged in." });

            // ✅ Prefer UserName, fallback to Email
            var sessionUser = (HttpContext.Session.GetString("UserName") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sessionUser))
                sessionUser = sessionEmail.Trim();

            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, message = "Code is required." });
            }

            string code = request.Code.Trim().ToUpper();
            string locationDesc = (request.LocationDesc ?? "").Trim();
            string remark1 = (request.Remark1 ?? "").Trim();
            string remark2 = (request.Remark2 ?? "").Trim();
            string remark3 = (request.Remark3 ?? "").Trim();

            try
            {
                string safeCode = EscapeSQL(code);
                string safeLocDesc = EscapeSQL(locationDesc);

                // 1) Convert LOCATION DESCRIPTION -> LOCATION CODE
                string locSql = $@"
SELECT FIRST 1 TRIM(CODE) AS CODE
FROM ST_LOCATION
WHERE UPPER(TRIM(DESCRIPTION)) = UPPER('{safeLocDesc}')
";
                var locRows = _dbHelper.ExecuteSelect(locSql);

                string locationCode = "";
                if (locRows != null && locRows.Count > 0 &&
                    locRows[0].ContainsKey("CODE") && locRows[0]["CODE"] != null)
                {
                    locationCode = locRows[0]["CODE"].ToString()?.Trim() ?? "";
                }

                if (string.IsNullOrWhiteSpace(locationCode))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Location not found. Please select a valid location."
                    });
                }

                string safeLocationCode = EscapeSQL(locationCode);

                // 2) Escape remarks
                string safeRemark1 = EscapeSQL(remark1);
                string safeRemark2 = EscapeSQL(remark2);
                string safeRemark3 = EscapeSQL(remark3);

                // 3) Current system datetime (server time)
                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string safeNowStr = EscapeSQL(nowStr);

                // 4) Session user -> UDF_USER
                string safeUser = EscapeSQL(sessionUser);

                // 5) Insert new row (DTLKEY NOT NULL)
                string insertSql = $@"
INSERT INTO ST_ITEM_TPLDTL
    (DTLKEY, CODE, ITEMCODE, LOCATION, REMARK1, REMARK2, UDF_REMARK3, UDF_DATETIME, UDF_USER)
VALUES
    (
      (SELECT COALESCE(MAX(DTLKEY), 0) + 1 FROM ST_ITEM_TPLDTL),
      '{safeCode}',
      '{safeCode}',
      '{safeLocationCode}',
      '{safeRemark1}',
      '{safeRemark2}',
      '{safeRemark3}',
      '{safeNowStr}',
      '{safeUser}'
    )
";

                _dbHelper.ExecuteNonQuery(insertSql);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Insert failed.", detail = ex.Message });
            }
        }

        private string EscapeSQL(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("'", "''");
        }

        // ----------------------------
        // Request Models
        // ----------------------------
        public class ScannerRequest
        {
            public string Code { get; set; } = "";
        }

        public class InsertDetailRequest
        {
            public string Code { get; set; } = "";
            public string? LocationDesc { get; set; }
            public string? Remark1 { get; set; }
            public string? Remark2 { get; set; }
            public string? Remark3 { get; set; }
        }
    }
}
