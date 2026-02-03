using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.DTOs;

namespace MyMascada.Infrastructure.Services.CsvImport;

/// <summary>
/// AI-powered CSV structure analysis service using LLM
/// </summary>
public class AICsvAnalysisService : IAICsvAnalysisService
{
    private readonly ILlmCategorizationService _llmService;
    private readonly IFeatureFlags _featureFlags;
    private readonly ILogger<AICsvAnalysisService> _logger;

    // Target fields for mapping
    private static readonly string[] TargetFields = { "date", "amount", "description", "type", "balance", "reference", "category" };

    public AICsvAnalysisService(
        ILlmCategorizationService llmService,
        IFeatureFlags featureFlags,
        ILogger<AICsvAnalysisService> logger)
    {
        _llmService = llmService;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    public async Task<CsvAnalysisResultDto> AnalyzeCsvStructureAsync(
        Stream csvStream, 
        string? accountType = null,
        string? currencyHint = null)
    {
        try
        {
            // Extract sample data from CSV
            var (headers, sampleRows) = await ExtractSampleDataAsync(csvStream);
            
            if (!headers.Any() || !sampleRows.Any())
            {
                return new CsvAnalysisResultDto
                {
                    Success = false,
                    ErrorMessage = "Unable to read CSV data or file is empty"
                };
            }

            // Step 1: Identify columns using AI (fallback to heuristics if AI not configured)
            var columnAnalysis = _featureFlags.AiCategorization
                ? await IdentifyColumnsWithAIAsync(headers, sampleRows, accountType)
                : FallbackColumnIdentification(headers, sampleRows);
            
            // Step 2: Detect amount convention
            var amountConvention = await DetectAmountConventionAsync(sampleRows, columnAnalysis);
            
            // Step 3: Detect date formats
            var dateFormats = await DetectDateFormatsAsync(sampleRows, columnAnalysis);
            
            // Step 4: Detect currency if not provided
            var detectedCurrency = currencyHint ?? await DetectCurrencyAsync(sampleRows, columnAnalysis);

            // Build the result
            var result = new CsvAnalysisResultDto
            {
                Success = true,
                AvailableColumns = headers,
                SampleRows = sampleRows,
                AmountConvention = amountConvention,
                DateFormats = dateFormats,
                DetectedCurrency = detectedCurrency,
                DetectedBankFormat = await DetectBankFormatAsync(headers, sampleRows)
            };

            if (!_featureFlags.AiCategorization)
            {
                result.Warnings.Add("AI analysis is not configured. Used heuristic column detection instead.");
            }

            // Convert AI analysis to mappings
            foreach (var field in TargetFields)
            {
                if (columnAnalysis.TryGetValue(field, out var mapping))
                {
                    var sampleValues = GetSampleValues(sampleRows, mapping.ColumnName, 3);
                    
                    // Special handling for type column - get ALL distinct values from entire file
                    if (field.Equals("type", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(mapping.ColumnName))
                    {
                        _logger.LogDebug("Extracting all distinct values for type column '{ColumnName}'", mapping.ColumnName);
                        var allDistinctValues = await ExtractAllDistinctValuesAsync(csvStream, mapping.ColumnName);
                        if (allDistinctValues.Any())
                        {
                            _logger.LogDebug("Found {Count} distinct type values: {Values}", allDistinctValues.Count, string.Join(", ", allDistinctValues));
                            // Replace sample values with all distinct values for type column
                            sampleValues = allDistinctValues;
                        }
                        else
                        {
                            _logger.LogWarning("No distinct values found for type column '{ColumnName}'", mapping.ColumnName);
                        }
                    }
                    
                    result.SuggestedMappings[field] = new ColumnMappingDto
                    {
                        CsvColumnName = mapping.ColumnName,
                        TargetField = field,
                        Confidence = mapping.Confidence,
                        Interpretation = GetFieldInterpretation(field, amountConvention),
                        SampleValues = sampleValues
                    };
                    result.ConfidenceScores[field] = mapping.Confidence;
                }
            }

            // Add any warnings
            AddAnalysisWarnings(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing CSV structure");
            return new CsvAnalysisResultDto
            {
                Success = false,
                ErrorMessage = $"Analysis failed: {ex.Message}"
            };
        }
    }

    public async Task<CsvMappingValidationResult> ValidateMappingsAsync(
        Stream csvStream,
        CsvColumnMappings mappings)
    {
        var result = new CsvMappingValidationResult { IsValid = true };

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);
            
            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord?.ToList() ?? new List<string>();

            // Validate mapped columns exist
            ValidateColumnExists(headers, mappings.DateColumn, "Date", result);
            ValidateColumnExists(headers, mappings.AmountColumn, "Amount", result);
            ValidateColumnExists(headers, mappings.DescriptionColumn, "Description", result);

            // Validate sample data
            var rowNumber = 0;
            while (await csv.ReadAsync() && rowNumber < 100) // Check first 100 rows
            {
                rowNumber++;
                var row = new Dictionary<string, string>();
                
                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header) ?? string.Empty;
                }

                var rowErrors = ValidateRow(row, mappings);
                if (rowErrors.Any())
                {
                    result.InvalidRowCount++;
                    if (result.InvalidRows.Count < 5) // Keep first 5 invalid rows
                    {
                        result.InvalidRows.Add(row);
                    }
                    result.Warnings.AddRange(rowErrors.Select(e => $"Row {rowNumber}: {e}"));
                }
                else
                {
                    result.ValidRowCount++;
                }
            }

            result.IsValid = result.Errors.Count == 0 && result.InvalidRowCount == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    private async Task<(List<string> headers, List<Dictionary<string, string>> rows)> ExtractSampleDataAsync(
        Stream csvStream, int sampleSize = 10)
    {
        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        try
        {
            csvStream.Position = 0;
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);
            
            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();
            headers = csv.HeaderRecord?.ToList() ?? new List<string>();

            // Read sample rows
            var rowCount = 0;
            while (await csv.ReadAsync() && rowCount < sampleSize)
            {
                var row = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header) ?? string.Empty;
                }
                rows.Add(row);
                rowCount++;
            }

            // Reset stream position for subsequent reads
            csvStream.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting sample data from CSV");
        }

        return (headers, rows);
    }

    private async Task<Dictionary<string, ColumnAnalysis>> IdentifyColumnsWithAIAsync(
        List<string> headers, 
        List<Dictionary<string, string>> sampleRows,
        string? accountType)
    {
        var prompt = BuildColumnIdentificationPrompt(headers, sampleRows, accountType);
        
        try
        {
            var response = await _llmService.SendPromptAsync(prompt);
            return ParseColumnAnalysisResponse(response, headers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI column identification failed, falling back to heuristics");
            return FallbackColumnIdentification(headers, sampleRows);
        }
    }

    private string BuildColumnIdentificationPrompt(
        List<string> headers,
        List<Dictionary<string, string>> sampleRows,
        string? accountType)
    {
        var csvSample = new StringBuilder();
        csvSample.AppendLine(string.Join(",", headers));
        
        foreach (var row in sampleRows.Take(5))
        {
            var values = headers.Select(h => row.GetValueOrDefault(h, "")).ToArray();
            csvSample.AppendLine(string.Join(",", values));
        }

        return $@"Analyze this CSV bank statement sample and identify which columns map to these fields:
- date: Transaction date
- amount: Transaction amount (money value)
- description: Transaction description/details
- type: Debit/Credit indicator or transaction type
- balance: Running balance (if present)
- reference: Reference/check number (if present)

Important considerations:
- Account Type: {accountType ?? "Unknown"}
- 'Type' columns might contain transaction categories OR debit/credit indicators
- Amounts might be negative for debits OR all positive with a separate indicator
- Analyze the actual data values, not just column names

CSV Sample:
```csv
{csvSample}
```

Respond with JSON only, no other text:
{{
  ""date"": {{""column"": ""column_name"", ""confidence"": 0.95}},
  ""amount"": {{""column"": ""column_name"", ""confidence"": 0.90}},
  ""description"": {{""column"": ""column_name"", ""confidence"": 0.85}},
  ""type"": {{""column"": ""column_name"", ""confidence"": 0.70}},
  ""balance"": {{""column"": ""column_name"", ""confidence"": 0.60}},
  ""reference"": {{""column"": ""column_name"", ""confidence"": 0.50}}
}}

If a field doesn't exist, use null for the column name.";
    }

    private Dictionary<string, ColumnAnalysis> ParseColumnAnalysisResponse(
        string aiResponse, 
        List<string> availableHeaders)
    {
        var result = new Dictionary<string, ColumnAnalysis>();

        try
        {
            // Extract JSON from response (in case AI added extra text)
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = aiResponse.Substring(jsonStart, jsonEnd - jsonStart);
                var json = JsonDocument.Parse(jsonStr);

                foreach (var field in json.RootElement.EnumerateObject())
                {
                    var fieldName = field.Name;
                    if (field.Value.TryGetProperty("column", out var columnElement) &&
                        field.Value.TryGetProperty("confidence", out var confidenceElement))
                    {
                        var columnName = columnElement.GetString();
                        var confidence = confidenceElement.GetDouble();

                        if (!string.IsNullOrEmpty(columnName) && 
                            availableHeaders.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                        {
                            result[fieldName] = new ColumnAnalysis
                            {
                                ColumnName = columnName,
                                Confidence = confidence
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI column analysis response");
        }

        return result;
    }

    private Dictionary<string, ColumnAnalysis> FallbackColumnIdentification(
        List<string> headers,
        List<Dictionary<string, string>> sampleRows)
    {
        var result = new Dictionary<string, ColumnAnalysis>();

        // Date patterns
        var datePatterns = new[] { "date", "transaction date", "posted", "trans date", "value date" };
        var dateColumn = headers.FirstOrDefault(h => 
            datePatterns.Any(p => h.Contains(p, StringComparison.OrdinalIgnoreCase)));
        
        if (dateColumn != null)
        {
            result["date"] = new ColumnAnalysis { ColumnName = dateColumn, Confidence = 0.8 };
        }

        // Amount patterns
        var amountPatterns = new[] { "amount", "debit", "credit", "value", "payment" };
        var amountColumn = headers.FirstOrDefault(h => 
            amountPatterns.Any(p => h.Contains(p, StringComparison.OrdinalIgnoreCase)));
        
        if (amountColumn != null)
        {
            result["amount"] = new ColumnAnalysis { ColumnName = amountColumn, Confidence = 0.8 };
        }

        // Description patterns
        var descPatterns = new[] { "description", "details", "particulars", "narrative", "memo" };
        var descColumn = headers.FirstOrDefault(h => 
            descPatterns.Any(p => h.Contains(p, StringComparison.OrdinalIgnoreCase)));
        
        if (descColumn != null)
        {
            result["description"] = new ColumnAnalysis { ColumnName = descColumn, Confidence = 0.8 };
        }

        // Type patterns
        var typePatterns = new[] { "type", "transaction type", "tran type", "category" };
        var typeColumn = headers.FirstOrDefault(h => 
            typePatterns.Any(p => h.Equals(h, StringComparison.OrdinalIgnoreCase)));
        
        if (typeColumn != null)
        {
            result["type"] = new ColumnAnalysis { ColumnName = typeColumn, Confidence = 0.6 };
        }

        return result;
    }

    private async Task<string> DetectAmountConventionAsync(
        List<Dictionary<string, string>> sampleRows,
        Dictionary<string, ColumnAnalysis> columnAnalysis)
    {
        if (!columnAnalysis.TryGetValue("amount", out var amountCol))
            return "unknown";

        var amountSamples = sampleRows
            .Select(r => r.GetValueOrDefault(amountCol.ColumnName, ""))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Take(10)
            .ToList();

        // Check if we have negative values
        var hasNegativeValues = amountSamples.Any(a => a.Contains('-'));
        
        // Check if we have a type column with D/C or Debit/Credit
        var hasTypeColumn = columnAnalysis.ContainsKey("type");
        
        if (hasNegativeValues)
        {
            return "negative-debits";
        }
        else if (hasTypeColumn)
        {
            // Check type column values
            var typeCol = columnAnalysis["type"];
            var typeValues = sampleRows
                .Select(r => r.GetValueOrDefault(typeCol.ColumnName, ""))
                .Distinct()
                .ToList();

            if (typeValues.Any(t => t.Equals("D", StringComparison.OrdinalIgnoreCase) || 
                                   t.Equals("C", StringComparison.OrdinalIgnoreCase) ||
                                   t.Contains("Debit", StringComparison.OrdinalIgnoreCase) ||
                                   t.Contains("Credit", StringComparison.OrdinalIgnoreCase)))
            {
                return "type-column";
            }
        }

        return "all-positive";
    }

    private async Task<List<string>> DetectDateFormatsAsync(
        List<Dictionary<string, string>> sampleRows,
        Dictionary<string, ColumnAnalysis> columnAnalysis)
    {
        if (!columnAnalysis.TryGetValue("date", out var dateCol))
            return new List<string> { "MM/dd/yyyy" }; // Default

        var dateSamples = sampleRows
            .Select(r => r.GetValueOrDefault(dateCol.ColumnName, ""))
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Take(5)
            .ToList();

        var detectedFormats = new List<string>();
        var commonFormats = new[]
        {
            "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "M/d/yyyy", "d/M/yyyy",
            "MM-dd-yyyy", "dd-MM-yyyy", "MMM dd, yyyy", "dd MMM yyyy"
        };

        foreach (var format in commonFormats)
        {
            if (dateSamples.All(d => DateTime.TryParseExact(d, format, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            {
                detectedFormats.Add(format);
            }
        }

        return detectedFormats.Any() ? detectedFormats : new List<string> { "MM/dd/yyyy" };
    }

    private async Task<string?> DetectCurrencyAsync(
        List<Dictionary<string, string>> sampleRows,
        Dictionary<string, ColumnAnalysis> columnAnalysis)
    {
        // Look for currency symbols in amount fields
        if (columnAnalysis.TryGetValue("amount", out var amountCol))
        {
            var amounts = sampleRows
                .Select(r => r.GetValueOrDefault(amountCol.ColumnName, ""))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

            if (amounts.Any(a => a.Contains('$'))) return "USD";
            if (amounts.Any(a => a.Contains('€'))) return "EUR";
            if (amounts.Any(a => a.Contains('£'))) return "GBP";
            if (amounts.Any(a => a.Contains('¥'))) return "JPY";
        }

        // Look for currency column
        var currencyPatterns = new[] { "currency", "curr", "ccy" };
        foreach (var header in sampleRows.FirstOrDefault()?.Keys.ToList() ?? new List<string>())
        {
            if (currencyPatterns.Any(p => header.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                var currencyValue = sampleRows.FirstOrDefault()?
                    .GetValueOrDefault(header, "")?.Trim().ToUpperInvariant();
                
                if (!string.IsNullOrEmpty(currencyValue) && currencyValue.Length == 3)
                {
                    return currencyValue;
                }
            }
        }

        return null; // Unable to detect
    }

    private async Task<string> DetectBankFormatAsync(
        List<string> headers,
        List<Dictionary<string, string>> sampleRows)
    {
        // Check for bank-specific patterns in headers and data
        var headerString = string.Join(",", headers).ToLowerInvariant();
        
        // Chase patterns
        if (headerString.Contains("chase") || 
            (headerString.Contains("posting date") && headerString.Contains("check or slip #")))
        {
            return "Chase Bank";
        }

        // Wells Fargo patterns
        if (headers.Any(h => h.Equals("Date", StringComparison.OrdinalIgnoreCase)) &&
            headers.Any(h => h.Equals("Amount", StringComparison.OrdinalIgnoreCase)) &&
            headers.Count == 5) // Wells Fargo typically has exactly 5 columns
        {
            return "Wells Fargo";
        }

        // Bank of America patterns
        if (headerString.Contains("bank of america") ||
            (headerString.Contains("posted date") && headerString.Contains("payee")))
        {
            return "Bank of America";
        }

        // Generic patterns
        if (headers.Any(h => h.Contains("Type", StringComparison.OrdinalIgnoreCase)) &&
            headers.Any(h => h.Contains("Details", StringComparison.OrdinalIgnoreCase)) &&
            headers.Any(h => h.Contains("Particulars", StringComparison.OrdinalIgnoreCase)))
        {
            return "ANZ Bank"; // Based on the sample you provided
        }

        return "Generic CSV";
    }

    private string GetFieldInterpretation(string field, string amountConvention)
    {
        return field switch
        {
            "amount" when amountConvention == "negative-debits" => 
                "Negative values represent expenses/debits, positive values represent income/credits",
            "amount" when amountConvention == "type-column" => 
                "All amounts are positive; use the type column to determine if it's a debit or credit",
            "type" => "Column indicating whether the transaction is a debit/credit or the transaction type",
            "date" => "Transaction date when the transaction was posted",
            "description" => "Description or details of the transaction",
            "balance" => "Running account balance after this transaction",
            "reference" => "Reference number, check number, or transaction ID",
            _ => $"Column mapped to {field}"
        };
    }

    private List<string> GetSampleValues(
        List<Dictionary<string, string>> rows, 
        string columnName, 
        int count)
    {
        return rows
            .Select(r => r.GetValueOrDefault(columnName, ""))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Take(count)
            .ToList();
    }

    private async Task<List<string>> ExtractAllDistinctValuesAsync(Stream csvStream, string columnName)
    {
        var distinctValues = new HashSet<string>();
        
        try
        {
            csvStream.Position = 0;
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);
            
            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();
            
            // Read all rows and collect distinct values for the specified column
            while (await csv.ReadAsync())
            {
                var value = csv.GetField(columnName)?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    distinctValues.Add(value);
                }
            }

            // Reset stream position for subsequent reads
            csvStream.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting distinct values for column {ColumnName}", columnName);
        }

        return distinctValues.OrderBy(v => v).ToList();
    }

    private void AddAnalysisWarnings(CsvAnalysisResultDto result)
    {
        // Check for low confidence mappings
        foreach (var mapping in result.SuggestedMappings)
        {
            if (mapping.Value.Confidence < 0.7)
            {
                result.Warnings.Add($"Low confidence ({mapping.Value.Confidence:P0}) for {mapping.Key} mapping. Please verify.");
            }
        }

        // Check for missing critical fields
        if (!result.SuggestedMappings.ContainsKey("date"))
        {
            result.Warnings.Add("Could not identify date column. Please select manually.");
        }

        if (!result.SuggestedMappings.ContainsKey("amount"))
        {
            result.Warnings.Add("Could not identify amount column. Please select manually.");
        }

        if (!result.SuggestedMappings.ContainsKey("description"))
        {
            result.Warnings.Add("Could not identify description column. Please select manually.");
        }

        // Warn about ambiguous type columns
        if (result.SuggestedMappings.TryGetValue("type", out var typeMapping) && 
            typeMapping.Confidence < 0.8)
        {
            result.Warnings.Add("The 'Type' column meaning is ambiguous. Please verify if it indicates debit/credit or transaction category.");
        }
    }

    private void ValidateColumnExists(
        List<string> headers, 
        string? columnName, 
        string fieldName,
        CsvMappingValidationResult result)
    {
        if (string.IsNullOrEmpty(columnName))
        {
            result.Errors.Add($"{fieldName} column is required but not mapped");
        }
        else if (!headers.Contains(columnName, StringComparer.OrdinalIgnoreCase))
        {
            result.Errors.Add($"{fieldName} column '{columnName}' does not exist in CSV");
        }
    }

    private List<string> ValidateRow(
        Dictionary<string, string> row,
        CsvColumnMappings mappings)
    {
        var errors = new List<string>();

        // Validate date
        if (!string.IsNullOrEmpty(mappings.DateColumn))
        {
            var dateValue = row.GetValueOrDefault(mappings.DateColumn, "");
            if (!DateTime.TryParseExact(dateValue, mappings.DateFormat, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
                !DateTime.TryParse(dateValue, out _))
            {
                errors.Add($"Invalid date format: '{dateValue}'");
            }
        }

        // Validate amount
        if (!string.IsNullOrEmpty(mappings.AmountColumn))
        {
            var amountValue = row.GetValueOrDefault(mappings.AmountColumn, "")
                .Replace("$", "").Replace(",", "").Replace("(", "-").Replace(")", "");
            
            if (!decimal.TryParse(amountValue, NumberStyles.Any, 
                CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"Invalid amount format: '{amountValue}'");
            }
        }

        // Validate description
        if (!string.IsNullOrEmpty(mappings.DescriptionColumn))
        {
            var description = row.GetValueOrDefault(mappings.DescriptionColumn, "");
            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add("Description is empty");
            }
        }

        return errors;
    }

    private class ColumnAnalysis
    {
        public string ColumnName { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}
