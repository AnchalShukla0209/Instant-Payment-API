using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Data;

namespace BeneficiaryImportConsole;

public class BeneficiaryImportService
{
    private readonly string _connectionString;

    public BeneficiaryImportService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> BulkInsertBeneficiariesFromExcel(string excelFilePath)
    {
        var beneficiaries = ReadExcelFile(excelFilePath);
        return await BulkInsertBeneficiaries(beneficiaries);
    }

    private List<BeneficiaryDto> ReadExcelFile(string filePath)
    {
        var beneficiaries = new List<BeneficiaryDto>();

        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension.Rows;

            for (int row = 2; row <= rowCount; row++) // Skip header row
            {
                try
                {
                    var createdOnText = worksheet.Cells[row, 7].Text?.Trim();
                    var updatedOnText = worksheet.Cells[row, 8].Text?.Trim();

                    // Get account number as string to avoid scientific notation
                    var accountNumberCell = worksheet.Cells[row, 3];
                    string accountNumber;
                    if (accountNumberCell.Value is double)
                    {
                        accountNumber = ((double)accountNumberCell.Value).ToString("F0");
                    }
                    else
                    {
                        accountNumber = accountNumberCell.Text?.Trim() ?? string.Empty;
                    }

                    // Get customer number as string to avoid scientific notation
                    var customerNumberCell = worksheet.Cells[row, 6];
                    string customerNumber;
                    if (customerNumberCell.Value is double)
                    {
                        customerNumber = ((double)customerNumberCell.Value).ToString("F0");
                    }
                    else
                    {
                        customerNumber = customerNumberCell.Text?.Trim() ?? string.Empty;
                    }

                    var beneficiary = new BeneficiaryDto
                    {
                        Status = bool.Parse(worksheet.Cells[row, 1].Text?.Trim() ?? "false"),
                        Name = worksheet.Cells[row, 2].Text?.Trim() ?? string.Empty,
                        AccountNumber = accountNumber,
                        BankName = worksheet.Cells[row, 4].Text?.Trim() ?? string.Empty,
                        Ifsc = worksheet.Cells[row, 5].Text?.Trim() ?? string.Empty,
                        CustomerNumber = customerNumber,
                        CreatedOn = string.IsNullOrEmpty(createdOnText) ? DateTime.UtcNow : DateTime.Parse(createdOnText),
                        UpdatedOn = string.IsNullOrEmpty(updatedOnText) ? DateTime.UtcNow : DateTime.Parse(updatedOnText)
                    };

                    beneficiaries.Add(beneficiary);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading row {row}: {ex.Message}");
                }
            }
        }

        return beneficiaries;
    }

    public async Task<int> BulkInsertBeneficiaries(List<BeneficiaryDto> beneficiaries)
    {
        if (beneficiaries == null || beneficiaries.Count == 0)
            return 0;

        var dataTable = new DataTable();
        dataTable.Columns.Add("status", typeof(bool));
        dataTable.Columns.Add("name", typeof(string));
        dataTable.Columns.Add("account_number", typeof(string));
        dataTable.Columns.Add("bank_name", typeof(string));
        dataTable.Columns.Add("ifsc", typeof(string));
        dataTable.Columns.Add("customer_number", typeof(string));
        dataTable.Columns.Add("createdOn", typeof(DateTime));
        dataTable.Columns.Add("updatedOn", typeof(DateTime));

        foreach (var beneficiary in beneficiaries)
        {
            dataTable.Rows.Add(
                beneficiary.Status,
                beneficiary.Name,
                beneficiary.AccountNumber,
                beneficiary.BankName,
                beneficiary.Ifsc,
                beneficiary.CustomerNumber,
                beneficiary.CreatedOn,
                beneficiary.UpdatedOn
            );
        }

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = "Beneficiaries";
                bulkCopy.BatchSize = 5000;
                bulkCopy.BulkCopyTimeout = 600;

                bulkCopy.ColumnMappings.Add("status", "status");
                bulkCopy.ColumnMappings.Add("name", "name");
                bulkCopy.ColumnMappings.Add("account_number", "account_number");
                bulkCopy.ColumnMappings.Add("bank_name", "bank_name");
                bulkCopy.ColumnMappings.Add("ifsc", "ifsc");
                bulkCopy.ColumnMappings.Add("customer_number", "customer_number");
                bulkCopy.ColumnMappings.Add("createdOn", "createdOn");
                bulkCopy.ColumnMappings.Add("updatedOn", "updatedOn");

                await bulkCopy.WriteToServerAsync(dataTable);
                return beneficiaries.Count;
            }
        }
    }
}
