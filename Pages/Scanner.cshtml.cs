using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FirebirdWeb.Helpers;
using System;
using System.Collections.Generic;

namespace FirebirdWeb.Pages
{
    public class ScannerModel : PageModel
    {
        private readonly DbHelper _db;

        public ScannerModel()
        {
            _db = new DbHelper(); // use your existing DbHelper
        }

        public void OnGet() { }

        [IgnoreAntiforgeryToken]
        public JsonResult OnPostValidateCode([FromBody] ScanRequest request)
        {
            // 1️⃣ Validate request
            if (request == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    cause = "MODEL_BINDING",
                    message = "Request body is null or invalid."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return new JsonResult(new
                {
                    success = false,
                    cause = "EMPTY_CODE",
                    message = "Scanned code is empty."
                });
            }

            try
            {
                // 2️⃣ Use DbHelper to query
                string sql = $"SELECT FIRST 1 DESCRIPTION FROM ST_ITEM_TPL WHERE TRIM(CODE) = '{request.Code.Trim()}'";

                var results = _db.ExecuteSelect(sql);

                if (results.Count > 0 && results[0].ContainsKey("DESCRIPTION"))
                {
                    string description = results[0]["DESCRIPTION"]?.ToString() ?? "";

                    return new JsonResult(new
                    {
                        success = true,
                        exists = true,
                        description
                    });
                }

                // Code not found
                return new JsonResult(new
                {
                    success = true,
                    exists = false,
                    message = "Code not found in database."
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new
                {
                    success = false,
                    cause = "DB_ERROR",
                    message = "Database query failed.",
                    detail = ex.Message
                });
            }
        }

        public class ScanRequest
        {
            public string Code { get; set; } = "";
        }
    }
}