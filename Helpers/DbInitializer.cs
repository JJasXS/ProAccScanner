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
    }
}
