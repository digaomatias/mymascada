using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.CsvImport.DTOs;

public class CsvImportRequest
{
    public int AccountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public CsvFormat Format { get; set; } = CsvFormat.Generic;
    public bool HasHeader { get; set; } = true;
    public bool SkipDuplicates { get; set; } = true;
    public bool AutoCategorize { get; set; } = true;
}

public class CsvImportResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SkippedRows { get; set; }
    public int ErrorRows { get; set; }
    public int ImportedTransactionsCount { get; set; }
    public int SkippedTransactionsCount { get; set; }
    public int DuplicateTransactionsCount { get; set; }
    public int? CreatedAccountId { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ImportedTransactionDto> ImportedTransactions { get; set; } = new();
}

public class ImportedTransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public bool IsNew { get; set; }
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
}

public class CsvTransactionRow
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Category { get; set; }
    public string? Notes { get; set; }
    public string? Type { get; set; } // Type column value from CSV
    public TransactionStatus Status { get; set; } = TransactionStatus.Cleared;
    public int RowNumber { get; set; }
    public string? ExternalId { get; set; }
}

public enum CsvFormat
{
    Generic = 0,
    Chase = 1,
    WellsFargo = 2,
    BankOfAmerica = 3,
    Mint = 4,
    Quicken = 5,
    ANZ = 6
}

public class CsvFieldMapping
{
    public int DateColumn { get; set; } = 0;
    public int DescriptionColumn { get; set; } = 1;
    public int AmountColumn { get; set; } = 2;
    public int? ReferenceColumn { get; set; }
    public int? CategoryColumn { get; set; }
    public int? NotesColumn { get; set; }
    public int? TypeColumn { get; set; } // Column containing transaction type (debit/credit)
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public bool IsAmountPositiveForDebits { get; set; } = false;
}

public class CsvParseResult
{
    public bool IsSuccess { get; set; }
    public List<CsvTransactionRow> Transactions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
}