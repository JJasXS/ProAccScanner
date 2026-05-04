using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using FirebirdWeb.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FirebirdWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly DbHelper _db;

        public SessionController(DbHelper db)
        {
            _db = db;
        }

        private static string EscapeSql(string? value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("'", "''");
        }

        /// <summary>Active display names for this email from SCAN_EMAIL_OPERATOR (may be empty).</summary>
        private List<string> LoadOperatorNamesForEmail(string email)
        {
            var list = new List<string>();
            var safe = EscapeSql((email ?? "").Trim());
            if (string.IsNullOrWhiteSpace(safe))
                return list;

            string sql = $@"
SELECT TRIM(COALESCE(O.DISPLAY_NAME, '')) AS DISPLAY_NAME
FROM SCAN_EMAIL_OPERATOR O
WHERE UPPER(TRIM(O.EMAIL)) = UPPER('{safe}')
  AND TRIM(COALESCE(O.DISPLAY_NAME, '')) <> ''
  AND (
    TRIM(UPPER(COALESCE(O.ISACTIVE, 'Y'))) IN ('Y', '1', 'T', 'TRUE')
    OR O.ISACTIVE IS NULL
  )
ORDER BY COALESCE(O.SORT_ORDER, 0), O.DISPLAY_NAME
";
            try
            {
                var rows = _db.ExecuteSelect(sql);
                if (rows == null) return list;
                foreach (var row in rows)
                {
                    if (!row.ContainsKey("DISPLAY_NAME") || row["DISPLAY_NAME"] == null) continue;
                    var n = row["DISPLAY_NAME"].ToString()?.Trim() ?? "";
                    if (n.Length > 0 && !list.Contains(n, System.StringComparer.OrdinalIgnoreCase))
                        list.Add(n);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SessionController] LoadOperatorNamesForEmail: " + ex.Message);
            }

            return list;
        }

        [HttpGet("scanner-operator")]
        public IActionResult GetScannerOperatorStatus()
        {
            var sqlName = (HttpContext.Session.GetString("UserName") ?? "").Trim();
            var email = (HttpContext.Session.GetString("UserEmail") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                email = (User.FindFirst(ClaimTypes.Email)?.Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sqlName))
                sqlName = (User.FindFirst(ClaimTypes.Name)?.Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sqlName))
                sqlName = email;

            var promptDone = HttpContext.Session.GetString(ScannerOperatorSessionKeys.PromptDone) == "1";
            var currentOperator = (HttpContext.Session.GetString(ScannerOperatorSessionKeys.OperatorName) ?? "").Trim();

            if (promptDone)
            {
                return Ok(new
                {
                    success = true,
                    promptDone = true,
                    selectorMode = "none",
                    operators = Array.Empty<object>(),
                    sqlFallbackName = sqlName,
                    currentOperator
                });
            }

            List<string> mapped;
            try
            {
                mapped = LoadOperatorNamesForEmail(email);
            }
            catch
            {
                mapped = new List<string>();
            }

            // No extra operators for this email → use SY_USER only for this session
            if (mapped.Count == 0)
            {
                HttpContext.Session.Remove(ScannerOperatorSessionKeys.OperatorName);
                HttpContext.Session.SetString(ScannerOperatorSessionKeys.PromptDone, "1");
                return Ok(new
                {
                    success = true,
                    promptDone = true,
                    selectorMode = "none",
                    operators = Array.Empty<object>(),
                    sqlFallbackName = sqlName,
                    currentOperator = ""
                });
            }

            // One or more mapped names → show popup (carousel) so user explicitly selects / confirms
            var ops = mapped.Select(d => new { displayName = d }).ToList();
            return Ok(new
            {
                success = true,
                promptDone = false,
                selectorMode = "carousel",
                operators = ops,
                sqlFallbackName = sqlName,
                currentOperator
            });
        }

        public class SetScannerOperatorRequest
        {
            /// <summary>"pick" (from SCAN_EMAIL_OPERATOR) or "syuser" (ignore mapping for this session).</summary>
            public string Mode { get; set; } = "";
            public string? DisplayName { get; set; }
        }

        [HttpPost("scanner-operator")]
        public IActionResult SetScannerOperator([FromBody] SetScannerOperatorRequest? body)
        {
            var email = (HttpContext.Session.GetString("UserEmail") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                email = (User.FindFirst(ClaimTypes.Email)?.Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { success = false, message = "Not logged in." });

            var mode = (body?.Mode ?? "").Trim().ToLowerInvariant();
            if (mode != "pick" && mode != "syuser")
                return BadRequest(new { success = false, message = "Invalid mode." });

            if (mode == "syuser")
            {
                HttpContext.Session.Remove(ScannerOperatorSessionKeys.OperatorName);
                HttpContext.Session.SetString(ScannerOperatorSessionKeys.PromptDone, "1");
                return Ok(new { success = true });
            }

            var pick = (body?.DisplayName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(pick))
                return BadRequest(new { success = false, message = "Select a name." });
            if (pick.Length > 120)
                return BadRequest(new { success = false, message = "Name is too long." });

            var allowed = LoadOperatorNamesForEmail(email);
            var ok = allowed.Any(a => string.Equals(a, pick, System.StringComparison.OrdinalIgnoreCase));
            if (!ok)
                return BadRequest(new { success = false, message = "That name is not allowed for this login." });

            var canonical = allowed.First(a => string.Equals(a, pick, System.StringComparison.OrdinalIgnoreCase));
            HttpContext.Session.SetString(ScannerOperatorSessionKeys.OperatorName, canonical);
            HttpContext.Session.SetString(ScannerOperatorSessionKeys.PromptDone, "1");
            return Ok(new { success = true });
        }
    }
}
