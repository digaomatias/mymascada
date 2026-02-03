using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.CsvImport;

public class CsvImportService : ICsvImportService
{
    public async Task<CsvParseResult> ParseCsvAsync(Stream csvStream, CsvFieldMapping mapping, bool hasHeader = true)
    {
        var result = new CsvParseResult();
        
        try
        {
            csvStream.Position = 0;
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            var records = new List<string[]>();
            
            // Read all records first
            while (await csv.ReadAsync())
            {
                var fieldCount = csv.Parser.Count;
                var record = new string[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    record[i] = csv.GetField(i) ?? string.Empty;
                }
                records.Add(record);
            }
            
            result.TotalRows = records.Count;
            
            // Skip header if present
            var dataRows = hasHeader ? records.Skip(1) : records;
            
            int rowNumber = hasHeader ? 2 : 1; // Start from 2 if header exists, 1 otherwise
            
            foreach (var record in dataRows)
            {
                try
                {
                    var transaction = ParseRow(record, mapping, rowNumber);
                    if (transaction != null)
                    {
                        result.Transactions.Add(transaction);
                        result.ValidRows++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                }
                
                rowNumber++;
            }
            
            result.IsSuccess = result.Errors.Count == 0 || result.ValidRows > 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse CSV: {ex.Message}");
            result.IsSuccess = false;
        }
        
        return result;
    }

    public CsvFieldMapping GetDefaultMapping(CsvFormat format)
    {
        return format switch
        {
            CsvFormat.Chase => new CsvFieldMapping
            {
                DateColumn = 0,           // Transaction Date
                DescriptionColumn = 2,    // Description  
                AmountColumn = 3,         // Amount
                ReferenceColumn = 1,      // Post Date
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            CsvFormat.WellsFargo => new CsvFieldMapping
            {
                DateColumn = 0,           // Date
                DescriptionColumn = 4,    // Description
                AmountColumn = 1,         // Amount
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            CsvFormat.BankOfAmerica => new CsvFieldMapping
            {
                DateColumn = 0,           // Date
                DescriptionColumn = 1,    // Description
                AmountColumn = 2,         // Amount
                ReferenceColumn = 3,      // Running Bal.
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            CsvFormat.Mint => new CsvFieldMapping
            {
                DateColumn = 0,           // Date
                DescriptionColumn = 1,    // Description
                AmountColumn = 3,         // Amount
                CategoryColumn = 2,       // Category
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            CsvFormat.Quicken => new CsvFieldMapping
            {
                DateColumn = 0,           // Date
                DescriptionColumn = 1,    // Description
                AmountColumn = 2,         // Amount
                CategoryColumn = 3,       // Category
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            CsvFormat.ANZ => new CsvFieldMapping
            {
                DateColumn = 6,           // Date column
                DescriptionColumn = 1,    // Details column
                AmountColumn = 5,         // Amount column
                ReferenceColumn = 0,      // Type column
                NotesColumn = 4,          // Reference column
                DateFormat = "dd/MM/yyyy",
                IsAmountPositiveForDebits = false
            },
            _ => new CsvFieldMapping
            {
                DateColumn = 0,
                DescriptionColumn = 1,
                AmountColumn = 2,
                DateFormat = "yyyy-MM-dd",
                IsAmountPositiveForDebits = false
            }
        };
    }

    public async Task<bool> ValidateFileAsync(Stream csvStream)
    {
        try
        {
            csvStream.Position = 0;
            using var reader = new StreamReader(csvStream, leaveOpen: true);
            
            // Read first few lines to validate format
            var firstLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(firstLine))
                return false;
                
            // Check if it looks like CSV (has commas)
            if (!firstLine.Contains(',') && !firstLine.Contains(';'))
                return false;
                
            // Try to parse with CsvHelper to validate format
            csvStream.Position = 0;
            using var csvReader = new CsvReader(new StreamReader(csvStream, leaveOpen: true), CultureInfo.InvariantCulture);
            
            // Try to read at least one record
            return await csvReader.ReadAsync();
        }
        catch
        {
            return false;
        }
    }

    public string GenerateExternalId(CsvTransactionRow row)
    {
        // Create a unique identifier based on date, amount, and description
        var content = $"{row.Date:yyyy-MM-dd}|{row.Amount}|{row.Description}";
        
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash)[..16]; // Take first 16 characters
    }

    private CsvTransactionRow? ParseRow(string[] record, CsvFieldMapping mapping, int rowNumber)
    {
        if (record.Length <= Math.Max(mapping.DateColumn, Math.Max(mapping.DescriptionColumn, mapping.AmountColumn)))
        {
            throw new InvalidOperationException($"Row has insufficient columns ({record.Length})");
        }

        var transaction = new CsvTransactionRow
        {
            RowNumber = rowNumber
        };

        // Parse date
        if (!DateTime.TryParseExact(record[mapping.DateColumn], mapping.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            if (!DateTime.TryParse(record[mapping.DateColumn], out date))
            {
                throw new InvalidOperationException($"Invalid date format: {record[mapping.DateColumn]}");
            }
        }
        // Ensure DateTime is treated as UTC for PostgreSQL compatibility
        transaction.Date = DateTime.SpecifyKind(date, DateTimeKind.Utc);

        // Parse description
        transaction.Description = record[mapping.DescriptionColumn].Trim();
        if (string.IsNullOrWhiteSpace(transaction.Description))
        {
            throw new InvalidOperationException("Description cannot be empty");
        }

        // Parse amount
        var amountText = record[mapping.AmountColumn].Replace("$", "").Replace(",", "").Trim();
        if (!decimal.TryParse(amountText, out var amount))
        {
            throw new InvalidOperationException($"Invalid amount format: {record[mapping.AmountColumn]}");
        }
        
        // Adjust sign based on bank format
        if (mapping.IsAmountPositiveForDebits && amount > 0)
        {
            amount = -amount; // Convert positive debits to negative
        }
        
        transaction.Amount = amount;

        // Parse optional fields
        if (mapping.ReferenceColumn.HasValue && mapping.ReferenceColumn.Value < record.Length)
        {
            transaction.Reference = record[mapping.ReferenceColumn.Value]?.Trim();
        }

        if (mapping.CategoryColumn.HasValue && mapping.CategoryColumn.Value < record.Length)
        {
            transaction.Category = record[mapping.CategoryColumn.Value]?.Trim();
        }

        if (mapping.NotesColumn.HasValue && mapping.NotesColumn.Value < record.Length)
        {
            transaction.Notes = record[mapping.NotesColumn.Value]?.Trim();
        }

        // Set default status and generate external ID
        transaction.Status = TransactionStatus.Cleared;
        transaction.ExternalId = GenerateExternalId(transaction);

        return transaction;
    }
}