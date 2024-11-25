using System.Text;
using UseCase1004;


class Program
{

    static async Task Main()
    {
        string csvFilePath = "abnormal_records.csv";
        string newCsvFilePath = Path.GetFileNameWithoutExtension(csvFilePath) + "_result.csv"; // Create new file name

        // Read the CSV file and process each row
        var lines = await File.ReadAllLinesAsync(csvFilePath);
        var updatedLines = new List<string>();

        foreach (var line in lines)
        {
            Console.WriteLine($"================================================================================================================================");
            var rowData = line.Split(',');
            Console.WriteLine($"Current focus {line}");
            var firstCell = rowData[0];
            var secondCell = rowData[1];
            string company = firstCell.Split('.')[0];
            if (company.Length > 0 && company[company.Length - 1] == '9')
            {
                company = company.Substring(0, company.Length - 1);
            }
            string formNo = secondCell;

            if (firstCell.Contains("1004"))
            {
                var result = "";
                 var investigation = new Investigate1004(company, formNo);
                    investigation.InvestigateGaiaForm();
                var companyCode = investigation.GetCompanyCodeByComIdAsync(investigation.companyId);
                Console.WriteLine(companyCode);
                // try
                // {
                //     var investigation = new Investigate1004(company, formNo);
                //     investigation.InvestigateGaiaForm();
                // }
                // catch (Exception ex)
                // {
                //     Console.WriteLine($"investigation Error");
                //     Console.WriteLine($"investigation Error");
                //     Console.WriteLine($"Error processing company {company}, formNo {formNo}: {ex.Message}");
                //     Console.WriteLine($"investigation Error");
                //     Console.WriteLine($"investigation Error");
                // }
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
            }
            else
            {
                Console.WriteLine($"Other formKind: {firstCell}, continue");
                updatedLines.Add(line);
                continue;
            }








            Console.WriteLine($"================================================================================================================================");
        }

        // Write the updated lines to the new CSV file
        await File.WriteAllLinesAsync(newCsvFilePath, updatedLines, Encoding.UTF8);

        Console.WriteLine($"CSV file updated successfully! New file created: {newCsvFilePath}");
    }
}
