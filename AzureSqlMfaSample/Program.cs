using Microsoft.Data.SqlClient;



class Program
{
    static async Task Main()
    {
        // Use your own server, database, user ID, and password.
        string connectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB";

        using (var connection = new SqlConnection(connectionString))
        {
            // Open the connection
            await connection.OpenAsync();

            Console.WriteLine("Connected to the database successfully!");

            // Your SQL operations here
            await ExecuteQueries(connection);
        }
    }

    private static async Task ExecuteQueries(SqlConnection connection)
    {
        // Parameters
        string company = "CARPLUS";
        string formNo = "25977";
        string kind = "1001";
        string formKind = $"{company}9.FORM.{kind}";

        // Query for recent storage
        string recentQuery = @"
            SELECT *
            FROM gbpm.PTSyncForm
            WHERE FormKind LIKE @FormKind
            AND FormNo IN (@FormNo)
            ORDER BY CreatedOn";

        using (SqlCommand cmd = new SqlCommand(recentQuery, connection))
        {
            cmd.Parameters.AddWithValue("@FormKind", "%" + formKind + "%");
            cmd.Parameters.AddWithValue("@FormNo", formNo);

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    // Process recent storage results
                    Console.WriteLine($"Recent Storage: {reader[0]}");
                }
            }
        }
    }
}

