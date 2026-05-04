using System;
using FirebirdSql.Data.FirebirdClient;

namespace FirebirdWeb.Helpers
{
    /// <summary>
    /// Runs once at application startup to ensure required DB triggers exist.
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// Creates (or replaces) the trigger that keeps ST_ITEM_TPL.UDF_LASTSCANNED
        /// in sync with the most recently inserted ST_ITEM_TPLDTL.UDF_DATETIME for
        /// each scanned item code.
        /// </summary>
        public static void EnsureTriggers(DbHelper db)
        {
            const string triggerName = "TRG_LASTSCANNED_AFTER_INSERT";

            // Firebird DDL for the trigger.
            // After every INSERT into ST_ITEM_TPLDTL, copy the scanner date to
            // ST_ITEM_TPL.UDF_LASTSCANNED for the matching CODE row.
            const string createTriggerSql = @"
CREATE OR ALTER TRIGGER TRG_LASTSCANNED_AFTER_INSERT
ACTIVE AFTER INSERT ON ST_ITEM_TPLDTL
POSITION 0
AS
BEGIN
  IF (NEW.UDF_DATETIME IS NOT NULL) THEN
    UPDATE ST_ITEM_TPL
    SET UDF_LASTSCANNED = CAST(SUBSTRING(NEW.UDF_DATETIME FROM 1 FOR 10) AS DATE)
    WHERE UPPER(TRIM(CODE)) = UPPER(TRIM(NEW.CODE));
END";

            try
            {
                db.ExecuteNonQuery(createTriggerSql);
                Console.WriteLine($"[DbInitializer] Trigger '{triggerName}' created/updated successfully.");
            }
            catch (Exception ex)
            {
                // Log but don't crash the app — the trigger may already exist with the same body.
                Console.WriteLine($"[DbInitializer] Warning: could not create trigger '{triggerName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Optional mapping: one login email → many operator display names (scanner picks one per session).
        /// If the email has no rows here, scans use SY_USER name only.
        /// </summary>
        public static void EnsureScanEmailOperatorTable(DbHelper db)
        {
            const string createTable = @"
CREATE TABLE SCAN_EMAIL_OPERATOR (
  EMAIL VARCHAR(250) NOT NULL,
  DISPLAY_NAME VARCHAR(120) NOT NULL,
  SORT_ORDER INTEGER DEFAULT 0,
  ISACTIVE VARCHAR(1) DEFAULT 'Y',
  CONSTRAINT PK_SCAN_EMAIL_OPR PRIMARY KEY (EMAIL, DISPLAY_NAME)
)";
            try
            {
                db.ExecuteNonQuery(createTable);
                Console.WriteLine("[DbInitializer] Table SCAN_EMAIL_OPERATOR created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbInitializer] SCAN_EMAIL_OPERATOR create (may already exist): {ex.Message}");
            }

            const string createIdx = @"
CREATE INDEX IDX_SCAN_EMAIL_OPR_EMAIL ON SCAN_EMAIL_OPERATOR (EMAIL)
";
            try
            {
                db.ExecuteNonQuery(createIdx);
                Console.WriteLine("[DbInitializer] Index IDX_SCAN_EMAIL_OPR_EMAIL created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbInitializer] SCAN_EMAIL_OPERATOR index (may already exist): {ex.Message}");
            }
        }
    }
}
