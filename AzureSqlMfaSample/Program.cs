using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

class Program
{
    public static List<PTSyncFormRecord> records = new List<PTSyncFormRecord>();
    public static Guid comId;
    public static Guid empId;
    public static string attendanceType;
    public static DateTimeOffset attendanceOn;
    public static string companyCode;
    public static Dictionary<Guid, string> data = new Dictionary<Guid, string>();

    public static bool AllIsEffectOne = false;

    static async Task Main()
    {
        string company = "FGLIFE";
        string formNo = "21779";

        await ReadCsvToDictionaryAsync("com_id_code.csv");
        // Use your own server, database, user ID, and password.
        string connectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB";


        using (var connection = new SqlConnection(connectionString))
        {
            // Open the connection
            await connection.OpenAsync();

            Console.WriteLine("Connected to the database successfully!");

            // Your SQL operations here
            await ExecuteQueries(connection, "gbpm.PTSyncForm", company, formNo);
            Console.WriteLine("--長期存放");
            await ExecuteQueries(connection, "gbpm.PTSyncForm_Archive_2024", company, formNo);

            // Execute the new query and store AttendanceType and AttendanceOn
            await ExecuteAttendanceQuery(connection, company, formNo);

        }

        await GetCompanyCodeByComId(comId);



        string comconnectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + $"Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaTube{companyCode}";


        using (var comconnection = new SqlConnection(comconnectionString))
        {
            // Open the connection
            await comconnection.OpenAsync();

            Console.WriteLine("Connected to the company database successfully!");
            bool allIsEffectOne = await isAllIsEffectOne(comconnection, comId, empId, attendanceOn, 1);
            if (allIsEffectOne)
            {
                await SendPostRequest();
            }
        }
    }

    private static async Task<string> UseCase(string company, string formNo)
    {
        // string company = "FGLIFE";
        // string formNo = "21779";

        await ReadCsvToDictionaryAsync("com_id_code.csv");
        // Use your own server, database, user ID, and password.
        string connectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB";


        using (var connection = new SqlConnection(connectionString))
        {
            // Open the connection
            await connection.OpenAsync();

            Console.WriteLine("Connected to the database successfully!");

            // Your SQL operations here
            await ExecuteQueries(connection, "gbpm.PTSyncForm", company, formNo);
            Console.WriteLine("--長期存放");
            await ExecuteQueries(connection, "gbpm.PTSyncForm_Archive_2024", company, formNo);

            // Execute the new query and store AttendanceType and AttendanceOn
            await ExecuteAttendanceQuery(connection, company, formNo);

        }

        await GetCompanyCodeByComId(comId);



        string comconnectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + $"Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaTube{companyCode}";


        using (var comconnection = new SqlConnection(comconnectionString))
        {
            // Open the connection
            await comconnection.OpenAsync();

            Console.WriteLine("Connected to the company database successfully!");
            bool allIsEffectOne = await isAllIsEffectOne(comconnection, comId, empId, attendanceOn, 1);
            if (allIsEffectOne)
            {
                return await SendPostRequest();
            }
        }

        return string.Empty;
    }

    private static async Task ExecuteQueries(SqlConnection connection, string tableName, string company, string formNo)
    {
        // Parameters
        string kind = "1001";
        string formKind = $"{company}9.FORM.{kind}";

        // Query for recent storage
        string recentQuery = $@"
    SELECT CompanyId, UserEmployeeId, FormContent, FormAction, CreatedOn, ModifiedOn, Flag, RetryCount
    FROM {tableName}
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
                    // Create a record from the reader
                    var record = new PTSyncFormRecord
                    {
                        CompanyId = reader.GetGuid(reader.GetOrdinal("CompanyId")),
                        UserEmployeeId = reader.GetGuid(reader.GetOrdinal("UserEmployeeId")),
                        FormContent = reader.GetString(reader.GetOrdinal("FormContent")),
                        FormAction = reader.GetByte(reader.GetOrdinal("FormAction")),
                        CreatedOn = reader.GetDateTime(reader.GetOrdinal("CreatedOn")),
                        ModifiedOn = reader.GetDateTime(reader.GetOrdinal("ModifiedOn")),
                        Flag = reader.GetByte(reader.GetOrdinal("Flag")),
                        RetryCount = reader.GetByte(reader.GetOrdinal("RetryCount"))
                    };

                    // Add the record to the list
                    records.Add(record);

                    // Print the record
                    Console.WriteLine($"CompanyId: {record.CompanyId}, UserEmployeeId: {record.UserEmployeeId}, Flag: {record.Flag}, RetryCount: {record.RetryCount}");

                    comId = record.CompanyId;
                    empId = record.UserEmployeeId;
                }
            }
        }
    }

    private static async Task ExecuteAttendanceQuery(SqlConnection connection, string company, string formNo)
    {
        // Parameters for the new query
        string kind = "1001";
        string formKind = $"{company}9.FORM.{kind}";

        // Define the table name directly (no need for dynamic SQL)
        string tableName = $"gbpm.{company}9FORM{kind}";

        // Query to get attendance data
        string attendanceQuery = $@"
    SELECT
        f.ATTENDANCETYPE AS AttendanceType,
        CONVERT(DATETIMEOFFSET, (CONVERT(NVARCHAR, f.[DATETIME], 126) + f.TIMEZONE)) AT TIME ZONE 'UTC' AS AttendanceOn
    FROM {tableName} f
    JOIN gbpm.fm_form_header h ON f.form_no = h.form_no
    WHERE h.form_kind = @formKind AND h.form_no = @formNo";

        using (SqlCommand cmd = new SqlCommand(attendanceQuery, connection))
        {
            // Add parameters to the command
            cmd.Parameters.AddWithValue("@formKind", formKind);
            cmd.Parameters.AddWithValue("@formNo", formNo);

            // Execute the query and retrieve the results
            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    attendanceType = reader.GetString(reader.GetOrdinal("AttendanceType"));
                    attendanceOn = reader.GetDateTimeOffset(reader.GetOrdinal("AttendanceOn"));

                    // Print the attendance data
                    Console.WriteLine($"AttendanceType: {attendanceType}, AttendanceOn: {attendanceOn}");
                }
            }
        }
    }


    private static async Task ReadCsvToDictionaryAsync(string filePath)
    {
        // Use StreamReader to read the file asynchronously
        using (var reader = new StreamReader(filePath))
        {
            while (!reader.EndOfStream)
            {
                // Read each line asynchronously
                var line = await reader.ReadLineAsync();

                // Split the line by comma to separate the UUID and the name
                var parts = line.Split(',');

                if (parts.Length == 2)
                {
                    // Parse the UUID and add it to the static data field with the associated name
                    if (Guid.TryParse(parts[0], out Guid id))
                    {
                        data[id] = parts[1];
                    }
                    else
                    {
                        Console.WriteLine($"Invalid UUID: {parts[0]}");
                    }
                }
            }
        }
        if (data.Count > 0)
        {
            var firstEntry = data.First(); // Get the first entry from the dictionary
            Console.WriteLine($"{firstEntry.Key}: {firstEntry.Value}");
        }
        else
        {
            Console.WriteLine("No data found.");
        }

    }

    private static async Task GetCompanyCodeByComId(Guid comId)
    {
        if (data.ContainsKey(comId))
        {
            companyCode = data[comId];
            Console.WriteLine($"Company Code for {comId}: {companyCode}");
        }
        else
        {
            Console.WriteLine("Company ID not found.");
        }
    }


    private static async Task<bool> isAllIsEffectOne(SqlConnection connection, Guid companyId, Guid employeeId, DateTimeOffset attendanceDate, int attendanceType)
    {
        string query = @"
        SELECT AttendanceHistoryId, iAttendanceType, IsEffect
        FROM pt.AttendanceHistory
        WHERE CompanyId = @companyId
        AND EmployeeId = @employeeId
        AND AttendanceDate = @attendanceDate
        AND iAttendanceType = @attendanceType";

        using (SqlCommand cmd = new SqlCommand(query, connection))
        {
            // Add parameters to the command
            cmd.Parameters.AddWithValue("@companyId", companyId);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            cmd.Parameters.AddWithValue("@attendanceDate", attendanceDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@attendanceType", attendanceType);
            Console.WriteLine("SqlCommand");
            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                bool allIsEffectOne = true;
                int count = 0;
                while (await reader.ReadAsync())
                {
                    bool isEffect = reader.GetBoolean(reader.GetOrdinal("IsEffect"));
                    Console.WriteLine($"AttendanceHistoryId: {reader.GetGuid(reader.GetOrdinal("AttendanceHistoryId"))}, IsEffect: {isEffect}");
                    count++;
                    if (!isEffect)
                    {
                        allIsEffectOne = false;
                    }
                }

                return allIsEffectOne && count > 0;
            }
        }
    }

    private static async Task<string> SendPostRequest()
    {
        if (records.Count > 0)
        {
            // Get the first record's FormContent
            var formContent = records[0].FormContent;

            // Prepare the POST request with the form content
            var client = new HttpClient();
            var requestUri = "https://pt-be.mayohr.com/api/anonymous/ReCheckInForm";

            // Prepare the request body with the correct Content-Type header
            var content = new StringContent(formContent, Encoding.UTF8, "application/json");

            // Send the POST request
            var response = await client.PostAsync(requestUri, content);

            // Ensure success or handle errors
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {responseBody}");
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var jsonResponse = JObject.Parse(responseBody);
                var status = jsonResponse["Error"]?["Status"]?.ToString();
                return status ?? string.Empty;
            }
        }
        else
        {
            Console.WriteLine("No records found.");
        }
        return string.Empty;
    }
}


public class PTSyncFormRecord
{
    public Guid CompanyId { get; set; }
    public Guid UserEmployeeId { get; set; }
    public string FormContent { get; set; }
    public int FormAction { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
    public int Flag { get; set; }
    public int RetryCount { get; set; }
    public int AttendanceType { get; set; } // Add this property
    public DateTimeOffset AttendanceOn { get; set; } // Add this property
}
