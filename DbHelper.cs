using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;

public class DbHelper
{
    private readonly string _connectionString;

    // Constructor sets the connection string
    public DbHelper()
    {
        _connectionString = @"User=SYSDBA;Password=masterkey;Database=D:\SQLData\DB\ACC-TEST.FDB;DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;Pooling=true;";
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
}