using System.Text;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Queries;

/// <summary>
/// Query to export all user transactions as a CSV file.
/// Supports optional filtering by date range and account.
/// </summary>
public class ExportTransactionsCsvQuery : IRequest<byte[]>
{
    public Guid UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? AccountId { get; set; }
}

public class ExportTransactionsCsvQueryHandler : IRequestHandler<ExportTransactionsCsvQuery, byte[]>
{
    private readonly ITransactionRepository _transactionRepository;

    public ExportTransactionsCsvQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<byte[]> Handle(ExportTransactionsCsvQuery request, CancellationToken cancellationToken)
    {
        // Re-use the existing filtered query with a very large page to fetch all records
        var query = new GetTransactionsQuery
        {
            UserId = request.UserId,
            Page = 1,
            PageSize = 100_000, // Practical upper limit for a single export
            AccountId = request.AccountId,
            StartDate = request.From,
            EndDate = request.To,
            SortBy = "TransactionDate",
            SortDirection = "desc"
        };

        var (transactions, _) = await _transactionRepository.GetFilteredAsync(query);

        // Build CSV content
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine("Date,Description,Amount,Currency,Category,Account,Notes,Type");

        foreach (var t in transactions)
        {
            var date = t.TransactionDate.ToString("yyyy-MM-dd");
            var description = EscapeCsv(t.UserDescription ?? t.Description);
            var amount = t.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var currency = EscapeCsv(t.Account?.Currency ?? "");
            var category = EscapeCsv(t.Category?.Name ?? "");
            var account = EscapeCsv(t.Account?.Name ?? "");
            var notes = EscapeCsv(t.Notes ?? "");
            var type = GetTypeLabel(t.Type);

            sb.AppendLine($"{date},{description},{amount},{currency},{category},{account},{notes},{type}");
        }

        // Return with UTF-8 BOM so Excel opens it correctly
        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return utf8WithBom.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // If value contains comma, double-quote, or newline, wrap in quotes and escape inner quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string GetTypeLabel(TransactionType type) => type switch
    {
        TransactionType.Income => "Income",
        TransactionType.Expense => "Expense",
        TransactionType.TransferComponent => "Transfer",
        _ => "Expense"
    };
}
