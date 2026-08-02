using SenderImportConsole;
using OfficeOpenXml;

// Set EPPlus license for version 7.x
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// Connection string for MS SQL Server
string connectionString = "Server=103.49.124.63,13324;Database=IpSenderList_DB;User Id=sa;Password=qdg1f0c81AOJJOby;TrustServerCertificate=True;";

// Path to your Excel file
string excelFilePath = @"C:\Users\dell\Downloads\InstantPayment.dmtusers.xlsx";

var importService = new SenderImportService(connectionString);

Console.WriteLine("Starting bulk import...");
Console.WriteLine($"Reading Excel file: {excelFilePath}");

try
{
    int insertedCount = await importService.BulkInsertSendersFromExcel(excelFilePath);
    Console.WriteLine($"Successfully inserted {insertedCount} records into the database.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error during import: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
