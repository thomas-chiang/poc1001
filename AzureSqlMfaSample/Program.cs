using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore.Query;
using Azure.Core;
using System.Linq.Expressions;

class Program
{

    static async Task Main()
    {
        string csvFilePath = "1001.csv";
        string newCsvFilePath = Path.GetFileNameWithoutExtension(csvFilePath) + "_result.csv"; // Create new file name

        // Read the CSV file and process each row
        var lines = await File.ReadAllLinesAsync(csvFilePath);
        var updatedLines = new List<string>();

        foreach (var line in lines)
        {
            Console.WriteLine($"================================================================================================================================");
            var parts = line.Split(',');
            string company = parts[0].Split('.')[0];
            if (!parts[0].Contains("1001"))
            {
                Console.WriteLine($"!!!!!!!formKind {parts[0]}, continue");
                updatedLines.Add(line);
                continue;
            }


            if (company.Length > 0 && company[company.Length - 1] == '9')
            {
                company = company.Substring(0, company.Length - 1); // Remove the last character '9'
            }

            string formNo = parts[1];

            Console.WriteLine($"company: {company}");
            Console.WriteLine($"formNo: {formNo}");

            var result = "";
            try
            {
                result = await UseCase(company, formNo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing company {company}, formNo {formNo}: {ex.Message}");
            }

            result = result.Replace(" ", "");
            if (result.Contains("已結算"))
            {
                result = result.Replace("忘打卡_", "");
            }


            if (!string.IsNullOrEmpty(result))
            {
                updatedLines.Add($"{line},{result},預防,不用處理");
            }
            else
            {
                updatedLines.Add(line); // No result, keep the original line
            }
            Console.WriteLine($"================================================================================================================================");
        }

        // Write the updated lines to the new CSV file
        await File.WriteAllLinesAsync(newCsvFilePath, updatedLines, Encoding.UTF8);

        Console.WriteLine($"CSV file updated successfully! New file created: {newCsvFilePath}");
    }

    private static async Task<string> UseCase(string company, string formNo)
    {
        string connectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB";

        Guid comId = new Guid();
        Guid empId = new Guid();
        List<PTSyncFormRecord> records;
        DateTimeOffset attendanceOn;
        string attendanceType;

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            Console.WriteLine("Connected to the database successfully!");
            var PTSyncFormResult = new Dictionary<string, object> { };
            PTSyncFormResult = await PTSyncFormQueries(connection, "gbpm.PTSyncForm", company, formNo);
            records = (List<PTSyncFormRecord>)PTSyncFormResult["records"];
            if (records.Count > 0)
            {
                comId = (Guid)PTSyncFormResult["comId"];
                empId = (Guid)PTSyncFormResult["empId"];
            }




            Console.WriteLine("--長期存放");
            var PTSyncFormResultFromColdStorage = new Dictionary<string, object> { };
            PTSyncFormResultFromColdStorage = await PTSyncFormQueries(connection, "gbpm.PTSyncForm_Archive_2024", company, formNo);
            if (records.Count == 0)
            {
                comId = (Guid)PTSyncFormResultFromColdStorage["comId"];
                empId = (Guid)PTSyncFormResultFromColdStorage["empId"];
            }

            var coldStorageRecords = (List<PTSyncFormRecord>)PTSyncFormResultFromColdStorage["records"];
            records.AddRange(coldStorageRecords);
            records = records.OrderBy(record => record.CreatedOn).ToList();


            var Related9FORMResult = new Dictionary<string, object> { };
            Related9FORMResult = await Related9FORMQuery(connection, company, formNo);
            attendanceType = (string)Related9FORMResult["attendanceType"];
            attendanceOn = (DateTimeOffset)Related9FORMResult["attendanceOn"];
        }


        string allCompanyConnectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaTubeManageDB";


        var companyCode = await GetCompanyCodeByComIdAsync(comId, new SqlConnection(allCompanyConnectionString));
        string comconnectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + $"Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaTube{companyCode}";

        try
        {
            using (var comconnection = new SqlConnection(comconnectionString))
            {
                await comconnection.OpenAsync();
            }
        }
        catch
        {
            comconnectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                + $"Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaTubeDB";
        }

        using (var comconnection = new SqlConnection(comconnectionString))
        {
            await comconnection.OpenAsync();

            Console.WriteLine("Connected to the company database successfully!");
            bool allIsEffectOne = await IsAllIsEffect(comconnection, comId, empId, attendanceOn, int.Parse(attendanceType), int.Parse(formNo));
            if (allIsEffectOne)
            {
                return await SendPostRequest(records);
            }
        }

        return string.Empty;
    }

    private static async Task<Dictionary<string, object>> PTSyncFormQueries(SqlConnection connection, string tableName, string company, string formNo)
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

        // Dictionary to hold the records and ids
        var result = new Dictionary<string, object>();
        var records = new List<PTSyncFormRecord>();

        using (SqlCommand cmd = new SqlCommand(recentQuery, connection))
        {
            cmd.Parameters.AddWithValue("@FormKind", "%" + formKind + "%");
            cmd.Parameters.AddWithValue("@FormNo", formNo);

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                Guid comId = Guid.Empty;
                Guid empId = Guid.Empty;

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

                    records.Add(record);

                    Console.WriteLine($"CompanyId: {record.CompanyId}, UserEmployeeId: {record.UserEmployeeId}, Flag: {record.Flag}, RetryCount: {record.RetryCount}");

                    result["comId"] = record.CompanyId;
                    result["empId"] = record.UserEmployeeId;
                }

                result["records"] = records;

            }
        }

        return result;
    }

    private static async Task<Dictionary<string, object>> Related9FORMQuery(SqlConnection connection, string company, string formNo)
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
        var result = new Dictionary<string, object>();
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
                    result["attendanceType"] = reader.GetString(reader.GetOrdinal("AttendanceType"));
                    result["attendanceOn"] = reader.GetDateTimeOffset(reader.GetOrdinal("AttendanceOn"));

                    Console.WriteLine($"AttendanceType: {result["attendanceType"]}, AttendanceOn: {result["attendanceOn"]}");
                }
            }
        }

        return result;
    }


    private static async Task<string> GetCompanyCodeByComIdAsync(Guid comId, SqlConnection connection)
    {
        string companyCode = "";
        string query = "SELECT CompanyCode FROM Company WHERE CompanyId = @CompanyId";

        try
        {
            using (SqlCommand cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@CompanyId", comId);

                // Open the connection if it's not already open
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    companyCode = result.ToString();
                    Console.WriteLine($"Company Code for {comId}: {companyCode}");
                }
                else
                {
                    Console.WriteLine("Company ID not found.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return companyCode;
    }


    private static async Task<bool> IsAllIsEffect(SqlConnection connection, Guid companyId, Guid employeeId, DateTimeOffset attendanceDate, int attendanceType, int fromNo)
    {
        Console.WriteLine("Query Company's table: AttendanceHistory");
        string query = @"
        SELECT AH.AttendanceHistoryId, 
               AH.iAttendanceType, 
               AH.IsEffect,
               AH.IsDeleted,  
               AHR.SourceFormNo
        FROM pt.AttendanceHistory AS AH
        LEFT JOIN pt.AttendanceHistoryRecord AS AHR
          ON AH.AttendanceHistoryId = AHR.AttendanceHistoryId
        WHERE AH.CompanyId = @companyId
          AND AH.EmployeeId = @employeeId
          AND AH.AttendanceDate = @attendanceDate
          AND AH.iAttendanceType = @attendanceType
          AND (AHR.SourceFormNo IS NULL OR AHR.SourceFormNo = @fromNo)";

        using (SqlCommand cmd = new SqlCommand(query, connection))
        {
            // Add parameters to the command
            cmd.Parameters.AddWithValue("@companyId", companyId);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            cmd.Parameters.AddWithValue("@attendanceDate", attendanceDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@attendanceType", attendanceType);
            cmd.Parameters.AddWithValue("@fromNo", fromNo);

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                bool allIsEffectOne = true;
                while (await reader.ReadAsync())
                {
                    bool isEffect = reader.GetBoolean(reader.GetOrdinal("IsEffect"));
                    bool IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted"));
                    Console.WriteLine($"AttendanceHistoryId: {reader.GetGuid(reader.GetOrdinal("AttendanceHistoryId"))}, IsEffect: {isEffect}, IsDeleted: {IsDeleted}");
                    if (!isEffect)
                    {
                        allIsEffectOne = false;
                    }
                }

                return allIsEffectOne;
            }
        }
    }

    private static async Task<string> SendPostRequest(List<PTSyncFormRecord> records)
    {
        if (records.Count > 0)
        {
            // Get the first record's FormContent
            var formContent = records[0].FormContent;
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(formContent);
            Console.WriteLine();
            Console.WriteLine();
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
                var title = jsonResponse["Error"]?["Title"]?.ToString();
                return "忘打卡_" + title;
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
    public required string FormContent { get; set; }
    public int FormAction { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
    public int Flag { get; set; }
    public int RetryCount { get; set; }
    public int AttendanceType { get; set; }
    public DateTimeOffset AttendanceOn { get; set; }
}
