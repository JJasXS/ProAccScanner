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
        // 1) Validate scanned code
        //
        // Flow:
        // ✅ STEP 1: Check ST_ITEM by CODE (and get DESCRIPTION)
        // ✅ STEP 1.5: AUTO-INSERT into ST_ITEM_TPL (CODE, DESCRIPTION) ONLY if missing
        // ✅ STEP 2 (optional): Get latest LOCATION code from ST_ITEM_TPLDTL
        // ✅ STEP 3 (optional): Translate LOCATION code -> ST_LOCATION.DESCRIPTION
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

            // ✅ scanner cleanup (also handles NBSP)
            string code = (request.Code ?? "")
                .Replace("\u00A0", " ")
                .Trim()
                .ToUpper();

            try
            {
                string safeCode = EscapeSQL(code);

                // ✅ STEP 1: validate existence from ST_ITEM (master stock item)
                // also pull DESCRIPTION so we can store into ST_ITEM_TPL / ST_ITEM_TPLDTL
                string itemSql = $@"
SELECT FIRST 1
    TRIM(COALESCE(I.CODE, '')) AS CODE,
    TRIM(COALESCE(I.DESCRIPTION, '')) AS DESCRIPTION
FROM ST_ITEM I
WHERE UPPER(TRIM(I.CODE)) = '{safeCode}'
";
                var itemRows = _dbHelper.ExecuteSelect(itemSql);

                if (itemRows == null || itemRows.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        exists = false,
                        message = "Code not found in Stock Item."
                    });
                }

                string itemDesc = "";
                if (itemRows[0].ContainsKey("DESCRIPTION") && itemRows[0]["DESCRIPTION"] != null)
                    itemDesc = itemRows[0]["DESCRIPTION"].ToString()?.Trim() ?? "";

                // ✅ STEP 1.5: AUTO-INSERT into ST_ITEM_TPL (template table)
                // Rule:
                // - If CODE already exists in ST_ITEM_TPL -> DO NOTHING
                // - If CODE does not exist in ST_ITEM_TPL -> INSERT (CODE, DESCRIPTION) once
                string tplCheckSql = $@"
SELECT FIRST 1 1 AS EXISTS_FLAG
FROM ST_ITEM_TPL T
WHERE UPPER(TRIM(T.CODE)) = '{safeCode}'
";
                var tplRows = _dbHelper.ExecuteSelect(tplCheckSql);

                if (tplRows == null || tplRows.Count == 0)
                {
                    // ✅ AUTO-INSERT happens here (only once per CODE)
                    string safeItemDesc = EscapeSQL(itemDesc);

                    string tplInsertSql = $@"
INSERT INTO ST_ITEM_TPL (CODE, DESCRIPTION)
VALUES ('{safeCode}', '{safeItemDesc}')
";
                    _dbHelper.ExecuteNonQuery(tplInsertSql);
                }
                // else: already exists -> skip insert, continue

                // ✅ STEP 2: Optional - get latest location code from ST_ITEM_TPLDTL
                string locationCode = "";
                string dtlSql = $@"
SELECT FIRST 1
    TRIM(COALESCE(D.LOCATION, '')) AS LOCATION_CODE
FROM ST_ITEM_TPLDTL D
WHERE UPPER(TRIM(D.CODE)) = '{safeCode}'
   OR UPPER(TRIM(D.ITEMCODE)) = '{safeCode}'
ORDER BY D.DTLKEY DESC
";
                var dtlRows = _dbHelper.ExecuteSelect(dtlSql);

                if (dtlRows != null && dtlRows.Count > 0 &&
                    dtlRows[0].ContainsKey("LOCATION_CODE") && dtlRows[0]["LOCATION_CODE"] != null)
                {
                    locationCode = dtlRows[0]["LOCATION_CODE"].ToString()?.Trim() ?? "";
                }

                // ✅ STEP 3: Optional - translate location code -> location description
                string locationDesc = "";
                if (!string.IsNullOrWhiteSpace(locationCode))
                {
                    string safeLoc = EscapeSQL(locationCode);

                    string locSql = $@"
SELECT FIRST 1
    TRIM(COALESCE(L.DESCRIPTION, '')) AS DESCRIPTION
FROM ST_LOCATION L
WHERE UPPER(TRIM(L.CODE)) = UPPER('{safeLoc}')
";
                    var locRows = _dbHelper.ExecuteSelect(locSql);

                    if (locRows != null && locRows.Count > 0 &&
                        locRows[0].ContainsKey("DESCRIPTION") && locRows[0]["DESCRIPTION"] != null)
                    {
                        locationDesc = locRows[0]["DESCRIPTION"].ToString()?.Trim() ?? "";
                    }
                }

                return Ok(new
                {
                    success = true,
                    exists = true,
                    description = itemDesc,      // optional debug/info for UI
                    locationCode,               // may be ""
                    location = locationDesc     // may be ""
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
        //
        // Runs when user clicks "Update" in UI.
        // ✅ includes DESCRIPTION (pulled from ST_ITEM)
        // ✅ UDF_USER = session user name/email
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

            string code = (request.Code ?? "")
                .Replace("\u00A0", " ")
                .Trim()
                .ToUpper();

            string locationDesc = (request.LocationDesc ?? "").Trim();
            string remark1 = (request.Remark1 ?? "").Trim();
            string remark2 = (request.Remark2 ?? "").Trim();
            string remark3 = (request.Remark3 ?? "").Trim();

            try
            {
                string safeCode = EscapeSQL(code);
                string safeLocDesc = EscapeSQL(locationDesc);

                // ✅ Pull DESCRIPTION from ST_ITEM for this code (so UI doesn't need to send it)
                string itemDesc = "";
                string itemSql = $@"
SELECT FIRST 1 TRIM(COALESCE(I.DESCRIPTION, '')) AS DESCRIPTION
FROM ST_ITEM I
WHERE UPPER(TRIM(I.CODE)) = '{safeCode}'
";
                var itemRows = _dbHelper.ExecuteSelect(itemSql);
                if (itemRows != null && itemRows.Count > 0 &&
                    itemRows[0].ContainsKey("DESCRIPTION") && itemRows[0]["DESCRIPTION"] != null)
                {
                    itemDesc = itemRows[0]["DESCRIPTION"].ToString()?.Trim() ?? "";
                }

                // ✅ Convert LOCATION DESCRIPTION -> LOCATION CODE
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

                // ✅ Escape values for SQL
                string safeLocationCode = EscapeSQL(locationCode);
                string safeItemDesc = EscapeSQL(itemDesc);

                string safeRemark1 = EscapeSQL(remark1);
                string safeRemark2 = EscapeSQL(remark2);
                string safeRemark3 = EscapeSQL(remark3);

                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string safeNowStr = EscapeSQL(nowStr);

                string safeUser = EscapeSQL(sessionUser);

                // ✅ Insert detail row into ST_ITEM_TPLDTL (history table)
                // Writes:
                // - CODE, ITEMCODE, DESCRIPTION
                // - LOCATION (location CODE)
                // - remarks
                // - datetime + user
                string insertSql = $@"
INSERT INTO ST_ITEM_TPLDTL
    (DTLKEY, CODE, ITEMCODE, DESCRIPTION, LOCATION, REMARK1, REMARK2, UDF_REMARK3, UDF_DATETIME, UDF_USER)
VALUES
    (
      (SELECT COALESCE(MAX(DTLKEY), 0) + 1 FROM ST_ITEM_TPLDTL),
      '{safeCode}',
      '{safeCode}',
      '{safeItemDesc}',
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

        // ----------------------------
        // Helper: escape single quotes
        // ----------------------------
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
