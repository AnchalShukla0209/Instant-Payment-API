using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Data;

namespace SenderImportConsole;

public class SenderImportService
{
    private readonly string _connectionString;

    public SenderImportService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> BulkInsertSendersFromExcel(string excelFilePath)
    {
        var senders = ReadExcelFile(excelFilePath);
        return await BulkInsertOrUpdateSenders(senders);
    }

    private List<SenderDto> ReadExcelFile(string filePath)
    {
        var senders = new List<SenderDto>();

        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension.Rows;

            for (int row = 2; row <= rowCount; row++) // Skip header row
            {
                try
                {
                    var isVerifiedText = worksheet.Cells[row, 4].Text?.Trim().ToLower();
                    var isKycVerified = !string.IsNullOrEmpty(isVerifiedText) && (isVerifiedText == "true" || isVerifiedText == "1" || isVerifiedText == "yes");

                    var sender = new SenderDto
                    {
                        SenderMobile = worksheet.Cells[row, 1].Text?.Trim() ?? string.Empty,
                        FirstName = worksheet.Cells[row, 2].Text?.Trim() ?? string.Empty,
                        LastName = worksheet.Cells[row, 3].Text?.Trim() ?? string.Empty,
                        Address = string.Empty,
                        Pincode = worksheet.Cells[row, 5].Text?.Trim() ?? string.Empty,
                        State = string.Empty,
                        IsKycVerified = isKycVerified,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow
                    };

                    senders.Add(sender);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading row {row}: {ex.Message}");
                }
            }
        }

        return senders;
    }

    public async Task<int> BulkInsertOrUpdateSenders(List<SenderDto> senders)
    {
        if (senders == null || senders.Count == 0)
            return 0;

        // Filter out invalid mobile numbers (empty, null, or non-numeric)
        var validSenders = senders.Where(s => !string.IsNullOrEmpty(s.SenderMobile) && s.SenderMobile.All(char.IsDigit) && s.SenderMobile.Length >= 10).ToList();
        Console.WriteLine($"Filtered out {senders.Count - validSenders.Count} invalid mobile numbers. Processing {validSenders.Count} valid senders.");
        senders = validSenders;

        if (senders.Count == 0)
            return 0;

        // Deduplicate senders from Excel (keep last occurrence)
        var uniqueSenders = new Dictionary<string, SenderDto>();
        foreach (var sender in senders)
        {
            uniqueSenders[sender.SenderMobile] = sender;
        }
        senders = uniqueSenders.Values.ToList();
        Console.WriteLine($"Deduplicated to {senders.Count} unique senders from Excel.");

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // Get existing mobile numbers
            var existingMobiles = new HashSet<string>();
            var getExistingQuery = "SELECT sender_mobile FROM Senders";
            using (var command = new SqlCommand(getExistingQuery, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var mobile = reader["sender_mobile"]?.ToString();
                    if (!string.IsNullOrEmpty(mobile))
                    {
                        existingMobiles.Add(mobile);
                    }
                }
            }

            // Separate into new and existing senders
            var newSenders = new List<SenderDto>();
            var existingSenders = new List<SenderDto>();

            foreach (var sender in senders)
            {
                if (existingMobiles.Contains(sender.SenderMobile))
                {
                    existingSenders.Add(sender);
                }
                else
                {
                    newSenders.Add(sender);
                }
            }

            int totalProcessed = 0;

            // Bulk insert new senders
            if (newSenders.Count > 0)
            {
                var dataTable = new DataTable();
                dataTable.Columns.Add("sender_mobile", typeof(string));
                dataTable.Columns.Add("first_name", typeof(string));
                dataTable.Columns.Add("last_name", typeof(string));
                dataTable.Columns.Add("address", typeof(string));
                dataTable.Columns.Add("pincode", typeof(string));
                dataTable.Columns.Add("state", typeof(string));
                dataTable.Columns.Add("is_kyc_verified", typeof(bool));
                dataTable.Columns.Add("created_on", typeof(DateTime));
                dataTable.Columns.Add("updated_on", typeof(DateTime));

                foreach (var sender in newSenders)
                {
                    dataTable.Rows.Add(
                        sender.SenderMobile,
                        sender.FirstName,
                        sender.LastName,
                        sender.Address,
                        sender.Pincode,
                        sender.State,
                        sender.IsKycVerified,
                        sender.CreatedOn,
                        sender.UpdatedOn
                    );
                }

                using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    bulkCopy.DestinationTableName = "Senders";
                    bulkCopy.BatchSize = 5000;
                    bulkCopy.BulkCopyTimeout = 600;

                    bulkCopy.ColumnMappings.Add("sender_mobile", "sender_mobile");
                    bulkCopy.ColumnMappings.Add("first_name", "first_name");
                    bulkCopy.ColumnMappings.Add("last_name", "last_name");
                    bulkCopy.ColumnMappings.Add("address", "address");
                    bulkCopy.ColumnMappings.Add("pincode", "pincode");
                    bulkCopy.ColumnMappings.Add("state", "state");
                    bulkCopy.ColumnMappings.Add("is_kyc_verified", "is_kyc_verified");
                    bulkCopy.ColumnMappings.Add("created_on", "created_on");
                    bulkCopy.ColumnMappings.Add("updated_on", "updated_on");

                    await bulkCopy.WriteToServerAsync(dataTable);
                    totalProcessed += newSenders.Count;
                    Console.WriteLine($"Inserted {newSenders.Count} new senders.");
                }
            }

            // Update existing senders
            if (existingSenders.Count > 0)
            {
                var updateQuery = @"
                    UPDATE Senders 
                    SET first_name = @FirstName, 
                        last_name = @LastName, 
                        address = @Address, 
                        pincode = @Pincode,
                        state = @State,
                        is_kyc_verified = @IsKycVerified,
                        updated_on = @UpdatedOn
                    WHERE sender_mobile = @SenderMobile";

                using (var command = new SqlCommand(updateQuery, connection))
                {
                    foreach (var sender in existingSenders)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@SenderMobile", sender.SenderMobile);
                        command.Parameters.AddWithValue("@FirstName", sender.FirstName);
                        command.Parameters.AddWithValue("@LastName", sender.LastName);
                        command.Parameters.AddWithValue("@Address", sender.Address);
                        command.Parameters.AddWithValue("@Pincode", sender.Pincode);
                        command.Parameters.AddWithValue("@State", sender.State);
                        command.Parameters.AddWithValue("@IsKycVerified", sender.IsKycVerified);
                        command.Parameters.AddWithValue("@UpdatedOn", sender.UpdatedOn);
                        await command.ExecuteNonQueryAsync();
                    }
                    totalProcessed += existingSenders.Count;
                    Console.WriteLine($"Updated {existingSenders.Count} existing senders.");
                }
            }

            return totalProcessed;
        }
    }
}
