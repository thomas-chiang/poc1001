using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace UseCase1004
{
    public class Investigate1004
    {
        public string company;
        public string formNo;

        public Investigate1004(string company, string formNo)
        {
            this.company = company;
            this.formNo = formNo;
        }

        public void InvestigateGaiaForm()
        {
            string connectionString = @"Server=sea-asia-tube-sqlsrv.database.windows.net;"
                                    + "Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB";

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

EXEC
('
SELECT 
    f.form_no SourceFormNo請假單單號, 
    h.form_kind SourceFormKind, 
    form_status, 
    f.[NAME] EmployeeId, 
    f.TIMEZONE, 
    DATEADD(hour, -8, f.created_on) UtcApplyDatetime, 
    f.STARTDATETIME, 
    f.ENDDATETIME, 
    f.USERTUBECOMPANYID, *
FROM gbpm.' + @company + '9FORM' + @formKind + ' f JOIN gbpm.fm_form_header h on f.form_no = h.form_no
WHERE h.form_kind = ''' + @company + '9.FORM.' + @formKind + '''
AND h.form_no = ' + @formNo + '

select
    N''用請假撤銷單單號查'',
    f.form_no as 請假撤銷單單號,
    h.form_kind,
    h.form_status,
    f.CANCELFORM as 請假單單號,
    f.[name] EmployeeId,
    f.USERTUBECOMPANYID,
    f.USERTUBEDEPARTMENTID
from gbpm.'+ @company +'9FORM1005 f
    join gbpm.fm_form_header h on h.form_no = f.form_no
where h.form_kind = ''' + @company + '9.FORM.1005'''
    and h.form_no = ' + @formNo + '
');";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(sqlQuery1, connection);
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    do
                    {
                        Console.WriteLine("---- Query Result ----");
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            Console.Write(reader.GetName(i) + "\t");
                        }
                        Console.WriteLine();

                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.Write(reader[i].ToString() + "\t");
                            }
                            Console.WriteLine();
                        }
                    } while (reader.NextResult());
                }
            }
        }

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
