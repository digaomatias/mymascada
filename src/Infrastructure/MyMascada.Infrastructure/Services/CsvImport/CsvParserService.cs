using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.CsvImport;

/// <summary>
/// Service for parsing CSV files into structured transaction data.
/// Pure parsing logic separated from import/database operations for reusability.
/// </summary>
public class CsvParserService : ICsvParserService
{
    public async Task<CsvParseResult> ParseCsvAsync(Stream csvStream, CsvFieldMapping mapping, bool hasHeader = true, int maxRows = 0)
    {
        var result = new CsvParseResult
        {
            IsSuccess = true,
            Transactions = new List<CsvTransactionRow>(),
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = hasHeader,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = context =>
                {
                    result.Warnings.Add($"Bad data found at row {context.Context.Parser.Row}: {context.RawRecord}");
                }
            };

            using var reader = new StringReader(await new StreamReader(csvStream).ReadToEndAsync());
            using var csv = new CsvReader(reader, config);

            var rowNumber = hasHeader ? 1 : 0; // Start from 1 if header exists
            var rowsProcessed = 0;

            while (await csv.ReadAsync() && (maxRows == 0 || rowsProcessed < maxRows))
            {
                rowNumber++;
                
                try
                {
                    var record = csv.Parser.Record;
                    if (record == null || record.Length == 0)
                        continue;

                    var transaction = ParseRow(record, mapping, rowNumber);
                    if (transaction != null)
                    {
                        result.Transactions.Add(transaction);
                        rowsProcessed++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error parsing row {rowNumber}: {ex.Message}");
                }
            }

            result.TotalRows = rowNumber - (hasHeader ? 1 : 0);
            result.ValidRows = result.Transactions.Count;
            
            if (result.Errors.Count > 0)
            {
                result.IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Errors.Add($"Failed to parse CSV: {ex.Message}");
        }

        return result;
    }

    public async Task<bool> ValidateFileAsync(Stream csvStream)
    {
        try
        {
            var content = await new StreamReader(csvStream).ReadToEndAsync();
            
            // Reset stream position
            csvStream.Position = 0;

            if (string.IsNullOrWhiteSpace(content))
                return false;

            // Check for basic CSV indicators
            if (!content.Contains(',') && !content.Contains(';'))
                return false;

            // Try to parse first record with CsvHelper
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true
            };

            using var reader = new StringReader(content);
            using var csv = new CsvReader(reader, config);
            
            return await csv.ReadAsync();
        }
        catch
        {
            return false;
        }
    }

    public string GenerateExternalId(CsvTransactionRow row)
    {
        var input = $"{row.Date:yyyy-MM-dd}|{row.Amount}|{row.Description}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    public CsvFieldMapping GetDefaultMapping(CsvFormat format)
    {
        return format switch
        {
            CsvFormat.Generic => new CsvFieldMapping
            {
                DateColumn = 0,
                DescriptionColumn = 1,
                AmountColumn = 2,
                ReferenceColumn = 3,
                DateFormat = "yyyy-MM-dd",
                IsAmountPositiveForDebits = false
            },
            
            CsvFormat.Chase => new CsvFieldMapping
            {
                DateColumn = 0, // Transaction Date
                DescriptionColumn = 2, // Description
                AmountColumn = 3, // Amount
                ReferenceColumn = null,
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = true
            },
            
            CsvFormat.WellsFargo => new CsvFieldMapping
            {
                DateColumn = 0, // Date
                AmountColumn = 1, // Amount
                DescriptionColumn = 3, // Description
                ReferenceColumn = 2, // Memo
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = true
            },
            
            CsvFormat.BankOfAmerica => new CsvFieldMapping
            {
                DateColumn = 0, // Date
                DescriptionColumn = 1, // Description
                AmountColumn = 2, // Amount
                ReferenceColumn = null,
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = true
            },
            
            CsvFormat.Mint => new CsvFieldMapping
            {
                DateColumn = 0, // Date
                DescriptionColumn = 1, // Description
                CategoryColumn = 2, // Category
                AmountColumn = 3, // Amount
                ReferenceColumn = null,
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            
            CsvFormat.Quicken => new CsvFieldMapping
            {
                DateColumn = 0, // Date
                DescriptionColumn = 1, // Description
                AmountColumn = 2, // Amount
                CategoryColumn = 3, // Category
                ReferenceColumn = null,
                DateFormat = "MM/dd/yyyy",
                IsAmountPositiveForDebits = false
            },
            
            CsvFormat.ANZ => new CsvFieldMapping
            {
                DateColumn = 6, // Date
                AmountColumn = 5, // Amount
                DescriptionColumn = 1, // Details
                ReferenceColumn = 4, // Reference
                DateFormat = "dd/MM/yyyy",
                IsAmountPositiveForDebits = true
            },
            
            _ => throw new ArgumentException($"Unsupported CSV format: {format}")
        };
    }

    public Dictionary<CsvFormat, string> GetSupportedFormats()
    {
        return new Dictionary<CsvFormat, string>
        {
            { CsvFormat.Generic, "Generic CSV (Date, Description, Amount, Reference)" },
            { CsvFormat.Chase, "Chase Bank" },
            { CsvFormat.WellsFargo, "Wells Fargo" },
            { CsvFormat.BankOfAmerica, "Bank of America" },
            { CsvFormat.Mint, "Mint" },
            { CsvFormat.Quicken, "Quicken" },
            { CsvFormat.ANZ, "ANZ Bank" }
        };
    }

    /// <summary>
    /// Parse individual CSV row into transaction data
    /// </summary>
    private CsvTransactionRow? ParseRow(string[] record, CsvFieldMapping mapping, int rowNumber)
    {
        try
        {
            if (record.Length <= Math.Max(mapping.DateColumn, Math.Max(mapping.DescriptionColumn, mapping.AmountColumn)))
            {
                return null; // Not enough columns
            }

            var transaction = new CsvTransactionRow
            {
                RowNumber = rowNumber
            };

            // Parse date
            if (DateTime.TryParseExact(record[mapping.DateColumn], mapping.DateFormat, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                transaction.Date = date;
            }
            else if (DateTime.TryParse(record[mapping.DateColumn], out date))
            {
                transaction.Date = date;
            }
            else
            {
                return null; // Invalid date
            }

            // Parse description
            transaction.Description = record[mapping.DescriptionColumn]?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(transaction.Description))
            {
                return null; // Description is required
            }

            // Parse amount
            var amountText = record[mapping.AmountColumn]?.Trim() ?? "0";
            // Remove common currency symbols and formatting
            amountText = amountText.Replace("$", "").Replace(",", "").Replace("(", "-").Replace(")", "");
            
            if (decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, 
                CultureInfo.InvariantCulture, out var amount))
            {
                // Apply bank-specific amount sign convention
                if (mapping.IsAmountPositiveForDebits && amount < 0)
                {
                    amount = -amount; // Convert negative debits to positive
                }
                transaction.Amount = amount;
            }
            else
            {
                return null; // Invalid amount
            }

            // Parse optional fields
            if (mapping.ReferenceColumn.HasValue && mapping.ReferenceColumn.Value < record.Length)
            {
                transaction.Reference = record[mapping.ReferenceColumn.Value]?.Trim();
            }

            if (mapping.CategoryColumn.HasValue && mapping.CategoryColumn.Value < record.Length)
            {
                transaction.Category = record[mapping.CategoryColumn.Value]?.Trim();
            }

            if (mapping.TypeColumn.HasValue && mapping.TypeColumn.Value < record.Length)
            {
                transaction.Type = record[mapping.TypeColumn.Value]?.Trim();
            }

            // Set default status
            transaction.Status = TransactionStatus.Cleared;
            
            // Generate external ID for deduplication
            transaction.ExternalId = GenerateExternalId(transaction);

            return transaction;
        }
        catch
        {
            return null; // Parsing failed
        }
    }
}