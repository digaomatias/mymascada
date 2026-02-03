using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.OfxImport.DTOs;

namespace MyMascada.Infrastructure.Services.OfxImport;

/// <summary>
/// Service for parsing OFX/QFX files into structured transaction data.
/// Pure parsing logic separated from import/database operations for reusability.
/// </summary>
public class OfxParserService : IOfxParserService
{
    private static readonly Regex OfxHeaderRegex = new(@"OFXHEADER:\s*(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex OfxTagRegex = new(@"<([^/>]+)>([^<]*)", RegexOptions.IgnoreCase);

    public async Task<OfxParseResult> ParseOfxFileAsync(string content)
    {
        return await Task.FromResult(ParseOfxFile(content));
    }

    public async Task<OfxParseResult> ParseOfxFileAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return ParseOfxFile(content);
    }

    public bool IsValidOfxFile(string content)
    {
        // Be more lenient with validation - some banks don't include all standard headers
        var hasOfxTag = content.Contains("<OFX>", StringComparison.OrdinalIgnoreCase);
        var hasTransactions = content.Contains("STMTTRN", StringComparison.OrdinalIgnoreCase);
        var hasBankOrCreditCard = content.Contains("BANKMSGSRSV1", StringComparison.OrdinalIgnoreCase) || 
                                  content.Contains("CREDITCARDMSGSRSV1", StringComparison.OrdinalIgnoreCase);
        
        // Allow files that have OFX tag and transactions, even if missing some headers
        return hasOfxTag && (hasTransactions || hasBankOrCreditCard);
    }

    public async Task<bool> IsValidOfxFileAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return IsValidOfxFile(content);
    }

    public string ExtractXmlContent(string content)
    {
        var startIndex = content.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
            return string.Empty;

        return content.Substring(startIndex);
    }

    /// <summary>
    /// Main parsing method - processes OFX content and extracts structured data
    /// </summary>
    public OfxParseResult ParseOfxFile(string content)
    {
        var result = new OfxParseResult();

        try
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                result.Errors.Add("OFX content is empty");
                return result;
            }

            // Check if it's a valid OFX file
            if (!IsValidOfxFile(content))
            {
                result.Errors.Add("File does not appear to be a valid OFX file");
                return result;
            }

            // Extract the XML portion
            var xmlContent = ExtractXmlContent(content);
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                result.Errors.Add("Could not extract XML content from OFX file");
                return result;
            }

            // Parse the XML
            var xmlDoc = ParseOfxXml(xmlContent);
            if (xmlDoc == null)
            {
                result.Errors.Add("Could not parse OFX XML content");
                return result;
            }

            // Extract account information
            result.AccountInfo = ExtractAccountInfo(xmlDoc, result);

            // Extract transactions
            result.Transactions = ExtractTransactions(xmlDoc, result);

            // Extract statement date range
            ExtractStatementDates(xmlDoc, result);

            result.Success = result.Errors.Count == 0;
            result.Message = result.Success 
                ? $"Successfully parsed {result.Transactions.Count} transactions"
                : "Failed to parse OFX file";
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing OFX file: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    private XDocument? ParseOfxXml(string xmlContent)
    {
        try
        {
            // First try to parse as-is in case it's already well-formed XML (like AMEX files)
            try
            {
                return XDocument.Parse(xmlContent);
            }
            catch
            {
                // If that fails, try converting to well-formed XML for traditional OFX format
                var wellFormedXml = ConvertToWellFormedXml(xmlContent);
                return XDocument.Parse(wellFormedXml);
            }
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"XML parsing error: {ex.Message}");
            Console.WriteLine($"First 500 chars of XML content: {xmlContent.Substring(0, Math.Min(500, xmlContent.Length))}");
            return null;
        }
    }

    private string ConvertToWellFormedXml(string ofxXml)
    {
        // This is a simplified conversion - a full implementation would need more robust parsing
        var lines = ofxXml.Split('\n');
        var result = new List<string>();
        var tagStack = new Stack<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;
            
            // Skip OFX header lines and the problematic OFX processing instruction
            if (trimmedLine.StartsWith("<?OFX") ||
                trimmedLine.StartsWith("OFXHEADER:") || 
                trimmedLine.StartsWith("DATA:") || 
                trimmedLine.StartsWith("VERSION:") || 
                trimmedLine.StartsWith("SECURITY:") || 
                trimmedLine.StartsWith("ENCODING:") || 
                trimmedLine.StartsWith("CHARSET:") || 
                trimmedLine.StartsWith("COMPRESSION:") || 
                trimmedLine.StartsWith("OLDFILEUID:") || 
                trimmedLine.StartsWith("NEWFILEUID:"))
                continue;

            // Handle opening tags
            if (trimmedLine.StartsWith("<") && !trimmedLine.EndsWith(">"))
            {
                // This is an opening tag with content
                var match = OfxTagRegex.Match(trimmedLine);
                if (match.Success)
                {
                    var tagName = match.Groups[1].Value;
                    var content = match.Groups[2].Value;
                    
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Self-closing tag with content
                        result.Add($"<{tagName}>{content}</{tagName}>");
                    }
                    else
                    {
                        // Opening tag
                        result.Add($"<{tagName}>");
                        tagStack.Push(tagName);
                    }
                }
            }
            else if (trimmedLine.StartsWith("<") && trimmedLine.EndsWith(">"))
            {
                // This is a complete tag
                result.Add(trimmedLine);
                
                // If it's not a self-closing tag and doesn't start with </, it's an opening tag
                if (!trimmedLine.StartsWith("</") && !trimmedLine.EndsWith("/>"))
                {
                    var tagName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    tagStack.Push(tagName);
                }
                else if (trimmedLine.StartsWith("</"))
                {
                    // Closing tag - pop from stack
                    if (tagStack.Count > 0)
                        tagStack.Pop();
                }
            }
            else
            {
                // Content line - might need a closing tag
                result.Add(trimmedLine);
            }
        }

        // Close any remaining open tags
        while (tagStack.Count > 0)
        {
            var tag = tagStack.Pop();
            result.Add($"</{tag}>");
        }

        return string.Join("\n", result);
    }

    private OfxAccountInfo? ExtractAccountInfo(XDocument xmlDoc, OfxParseResult result)
    {
        try
        {
            // Check for bank account
            var bankAcctFrom = xmlDoc.Descendants("BANKACCTFROM").FirstOrDefault();
            
            // Check for credit card account if bank account not found
            var ccAcctFrom = xmlDoc.Descendants("CCACCTFROM").FirstOrDefault();
            
            if (bankAcctFrom == null && ccAcctFrom == null)
            {
                result.Warnings.Add("No account information found in OFX file (neither BANKACCTFROM nor CCACCTFROM)");
                return null;
            }

            var accountInfo = new OfxAccountInfo();
            
            if (bankAcctFrom != null)
            {
                // Bank account
                accountInfo.BankId = bankAcctFrom.Element("BANKID")?.Value ?? "";
                accountInfo.AccountId = bankAcctFrom.Element("ACCTID")?.Value ?? "";
                accountInfo.AccountType = bankAcctFrom.Element("ACCTTYPE")?.Value ?? "";
            }
            else if (ccAcctFrom != null)
            {
                // Credit card account
                accountInfo.AccountId = ccAcctFrom.Element("ACCTID")?.Value ?? "";
                accountInfo.AccountType = "CREDITCARD";
                // Credit cards don't have BANKID
            }
            
            accountInfo.Currency = xmlDoc.Descendants("CURDEF").FirstOrDefault()?.Value ?? "USD";

            // Extract balance information
            var ledgerBal = xmlDoc.Descendants("LEDGERBAL").FirstOrDefault();
            if (ledgerBal != null)
            {
                if (decimal.TryParse(ledgerBal.Element("BALAMT")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var balance))
                {
                    accountInfo.Balance = balance;
                }

                var dateValue = ledgerBal.Element("DTASOF")?.Value;
                if (!string.IsNullOrEmpty(dateValue))
                {
                    // Try parsing with full datetime format first
                    if (dateValue.Length >= 14)
                    {
                        var dateTimePart = dateValue.Substring(0, 14);
                        if (DateTime.TryParseExact(dateTimePart, "yyyyMMddHHmmss", 
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var balanceDate))
                        {
                            accountInfo.BalanceDate = balanceDate;
                        }
                    }
                    
                    // Fallback to date only if datetime parsing failed
                    if (accountInfo.BalanceDate == null && dateValue.Length >= 8)
                    {
                        var datePart = dateValue.Substring(0, 8);
                        if (DateTime.TryParseExact(datePart, "yyyyMMdd", 
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var balanceDate))
                        {
                            accountInfo.BalanceDate = balanceDate;
                        }
                    }
                }
            }

            return accountInfo;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not extract account information: {ex.Message}");
            return null;
        }
    }

    private List<OfxTransaction> ExtractTransactions(XDocument xmlDoc, OfxParseResult result)
    {
        var transactions = new List<OfxTransaction>();

        try
        {
            var stmtTrns = xmlDoc.Descendants("STMTTRN");

            foreach (var stmtTrn in stmtTrns)
            {
                try
                {
                    var transaction = new OfxTransaction
                    {
                        TransactionType = stmtTrn.Element("TRNTYPE")?.Value ?? "",
                        TransactionId = stmtTrn.Element("FITID")?.Value ?? "",
                        Name = stmtTrn.Element("NAME")?.Value ?? "",
                        Memo = stmtTrn.Element("MEMO")?.Value,
                        CheckNumber = stmtTrn.Element("CHECKNUM")?.Value,
                        ReferenceNumber = stmtTrn.Element("REFNUM")?.Value
                    };

                    // Parse amount
                    if (decimal.TryParse(stmtTrn.Element("TRNAMT")?.Value, NumberStyles.Float, 
                        CultureInfo.InvariantCulture, out var amount))
                    {
                        transaction.Amount = amount;
                    }

                    // Parse date
                    var dateStr = stmtTrn.Element("DTPOSTED")?.Value;
                    if (!string.IsNullOrEmpty(dateStr))
                    {
                        // OFX dates are in format YYYYMMDDHHMMSS[.sss][timezone]
                        // Extract just the date part (first 8 characters)
                        var datePart = dateStr.Length >= 8 ? dateStr.Substring(0, 8) : dateStr;
                        
                        if (DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, 
                            DateTimeStyles.None, out var postedDate))
                        {
                            transaction.PostedDate = postedDate;
                        }
                        else
                        {
                            // Fallback: try to parse the full date string
                            if (DateTime.TryParse(dateStr, out var fallbackDate))
                            {
                                transaction.PostedDate = fallbackDate;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(transaction.TransactionId))
                    {
                        transactions.Add(transaction);
                    }
                    else
                    {
                        result.Warnings.Add("Skipped transaction without FITID");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Could not parse transaction: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error extracting transactions: {ex.Message}");
        }

        return transactions;
    }

    private void ExtractStatementDates(XDocument xmlDoc, OfxParseResult result)
    {
        try
        {
            // Check for bank transaction list or credit card transaction list
            var banktranlist = xmlDoc.Descendants("BANKTRANLIST").FirstOrDefault() 
                              ?? xmlDoc.Descendants("CCSTMTRS").FirstOrDefault();
            
            if (banktranlist != null)
            {
                var startDateStr = banktranlist.Element("DTSTART")?.Value;
                var endDateStr = banktranlist.Element("DTEND")?.Value;

                if (!string.IsNullOrEmpty(startDateStr))
                {
                    var datePart = startDateStr.Length >= 8 ? startDateStr.Substring(0, 8) : startDateStr;
                    if (DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var startDate))
                    {
                        result.StatementStartDate = startDate;
                    }
                }

                if (!string.IsNullOrEmpty(endDateStr))
                {
                    var datePart = endDateStr.Length >= 8 ? endDateStr.Substring(0, 8) : endDateStr;
                    if (DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var endDate))
                    {
                        result.StatementEndDate = endDate;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not extract statement dates: {ex.Message}");
        }
    }
}