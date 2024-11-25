using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace UseCase1004
{
    public class Investigate1004
    {
        public string company;
        public string formNo;

        public string companyId;

        public Investigate1004(string company, string formNo)
        {
            this.company = company;
            this.formNo = formNo;
        }

        public void InvestigateGaiaForm()
        {
            string connectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                    + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB";
            string tableName = $"gbpm.{company}9FORM1004";
            string formKind = $"'{company}9.FORM.1004'";

            string sqlQuery1 = $@"
DECLARE @company NVARCHAR(30) = '{company}';
DECLARE @formNo INT = {formNo};
DECLARE @formKind NVARCHAR(30) = 1004;

-- 查詢 PTSyncForm_Archive_2024 拋轉狀況
SELECT *
FROM gbpm.PTSyncForm_Archive_2024
WHERE FormKind LIKE '%' + @company + '9.FORM.' + @formKind + '%'
    AND FormNo in (@formNo)
ORDER BY CreatedOn;

-- 查詢 PTSyncForm 拋轉狀況
SELECT *
FROM gbpm.PTSyncForm
WHERE FormKind LIKE '%' + @company + '9.FORM.' + @formKind + '%'
    AND FormNo in (@formNo)
ORDER BY CreatedOn;

SELECT 
    f.form_no SourceFormNo請假單單號, 
    h.form_kind SourceFormKind, 
    form_status, 
    f.[NAME] EmployeeId, 
    f.TIMEZONE, 
    DATEADD(hour, -8, f.created_on) UtcApplyDatetime, 
    f.STARTDATETIME, 
    f.ENDDATETIME, 
    f.USERTUBECOMPANYID
FROM  {tableName} f
JOIN gbpm.fm_form_header h ON f.form_no = h.form_no
WHERE h.form_kind = {formKind}
AND h.form_no = @formNo;
";
            // Console.WriteLine(sqlQuery1);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(sqlQuery1, connection);
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    do
                    {
                        Console.WriteLine("---- Query Result ----");

                        while (reader.Read())
                        {
                            Console.WriteLine();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.WriteLine(reader.GetName(i) + " : " + reader[i].ToString() + "\t");
                                if (reader.GetName(i) == "CompanyId")
                                {
                                    companyId = reader[i].ToString();
                                }
                            }
                            Console.WriteLine();
                        }
                    } while (reader.NextResult());
                }
            }
        }


        public string GetCompanyCodeByComIdAsync(string comId)
        {
            string companyCode = "";
            string query = "SELECT CompanyCode FROM Company WHERE CompanyId = @CompanyId";

            string allCompanyConnectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                    + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaTubeManageDB";


            SqlConnection connection = new SqlConnection(allCompanyConnectionString);

            try
            {
                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", comId);

                    // Open the connection if it's not already open
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    var result = cmd.ExecuteScalar();
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

        public string 

        public string Company
        {
            get { return company; }
        }

        public string FormNo
        {
            get { return formNo; }
        }
    }
}
