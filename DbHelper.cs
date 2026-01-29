using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;

namespace FirebirdWeb.Helpers
{
    public class DbHelper
    {
        private readonly string _connectionString;

        // Constructor sets the connection string
        public DbHelper()
        {
        
        //Dbeaver connect for testing (no lcoation data)
        //_connectionString = @"User=SYSDBA;Password=masterkey;Database=D:\SQLData\DB\ACC-ProAcc202601.FDB;DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;Pooling=true;";
        
        //SQL's connection (got locaiton data)
        _connectionString = @"User=SYSDBA;Password=masterkey;Database=localhost:C:\eStream\SQLAccounting\DB\ACC-PROACC202601.FDB;Port=3050;Dialect=3;Charset=UTF8;Pooling=true;";
        
        
        
        //_connectionString = @"User=ADMIN;Password=Jus@230526H;Database=C:\eStream\SQLAccounting\DB\ACC-PROACC202601.FDB;DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;Pooling=true;";
        }

        // Method to open and return a connection (optional, if needed elsewhere)
        public FbConnection GetConnection()
        {
            var conn = new FbConnection(_connectionString);
            conn.Open();
            return conn; // Caller must dispose
        }

        // Method to execute a SELECT query and return results
        public List<Dictionary<string, object>> ExecuteSelect(string sql)
        {
            var results = new List<Dictionary<string, object>>();

            using (var conn = new FbConnection(_connectionString))
            {
                conn.Open();

                using (var cmd = new FbCommand(sql, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader.GetValue(i);
                            }
                            results.Add(row);
                        }
                    }
                }
            }

            return results;
        }

        // Method to execute INSERT, UPDATE, DELETE queries
        public int ExecuteNonQuery(string sql)
        {
            try
            {
                Console.WriteLine($"[DB QUERY] Executing: {sql}");
                using (var conn = new FbConnection(_connectionString))
                {
                    conn.Open();
                    Console.WriteLine("[DB] Connection opened successfully");

                    using (var cmd = new FbCommand(sql, conn))
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();
                        Console.WriteLine($"[DB] Query executed successfully. Rows affected: {rowsAffected}");
                        return rowsAffected;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] Failed to execute query: {sql}");
                Console.WriteLine($"[DB ERROR] Error: {ex.Message}");
                throw; // Re-throw to let caller handle it
            }
        }
    }
}