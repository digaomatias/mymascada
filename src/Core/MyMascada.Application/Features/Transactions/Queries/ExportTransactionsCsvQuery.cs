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
        // Paginate through all transactions to ensure complete export regardless of count
        const int pageSize = 1000;
        var allTransactions = new List<Domain.Entities.Transaction>();
        int page = 1;
        int totalCount;

        do
        {
            var query = new GetTransactionsQuery
            {
                UserId = request.UserId,
                Page = page,
                PageSize = pageSize,
                AccountId = request.AccountId,
                StartDate = request.From,
                EndDate = request.To,
                SortBy = "TransactionDate",
                SortDirection = "desc"
            };

            var (transactions, count) = await _transactionRepository.GetFilteredAsync(query);
            totalCount = count;
            allTransactions.AddRange(transactions);
            page++;
        } while (allTransactions.Count < totalCount);

        // Build CSV content
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine("Date,Description,Amount,Currency,Category,Account,Notes,Type");

        foreach (var t in allTransactions)
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

        // Sanitize formula-triggering characters to prevent CSV injection
        char[] formulaTriggers = { '=', '+', '-', '@' };
        if (formulaTriggers.Any(c => value.StartsWith(c)))
            value = "'" + value;

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
        _ => throw new ArgumentOutOfRangeException(nameof(type), $"Not supported transaction type: {type}")
    };
}
