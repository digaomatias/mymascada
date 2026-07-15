using NSubstitute;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.ImportReview.DTOs;
using MyMascada.Application.Features.ImportReview.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Features.ImportReview;

public class ImportAnalysisServiceConflictTests
{
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly IApplicationLogger<ImportAnalysisService> _logger = Substitute.For<IApplicationLogger<ImportAnalysisService>>();

    private ImportAnalysisService CreateService() => new(_transactionRepository, _logger);

    /// <summary>
    /// Regression for the bank-sync crash "Value was either too large or too small for a
    /// Decimal". Bank syncs run with DateToleranceDays = 0 (exact date match). When an
    /// incoming candidate exactly matches an existing transaction on date and amount, the
    /// confidence calculation divided daysDifference by DateToleranceDays == 0, producing
    /// double.NaN, which threw OverflowException when cast to decimal. The analysis must
    /// complete and flag the duplicate instead of throwing.
    /// </summary>
    [Fact]
    public async Task AnalyzeImportAsync_ExactDateAndAmountMatch_WithZeroDateTolerance_DoesNotThrowAndFlagsDuplicate()
    {
        var accountId = 7;
        var userId = Guid.NewGuid();
        var date = new DateTime(2026, 07, 10, 0, 0, 0, DateTimeKind.Utc);

        // Existing expense of -42.50 on the same day, with a different external id so the
        // exact-external-id short-circuit does not apply and we reach the date/amount branch.
        var existing = new Transaction
        {
            Id = 100,
            AccountId = accountId,
            Amount = -42.50m,
            Description = "Existing coffee",
            TransactionDate = date,
            Source = TransactionSource.Manual,
            Status = TransactionStatus.Cleared,
            ExternalId = null
        };

        _transactionRepository
            .GetTransactionsByDateRangeAsync(accountId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), userId)
            .Returns(new List<Transaction> { existing });

        var candidate = new ImportCandidateDto
        {
            TempId = "c1",
            Amount = 42.50m,           // normalized to -42.50 for an expense
            Date = date,
            Description = "Coffee",
            Type = TransactionType.Expense,
            ExternalReferenceId = "akahu-tx-abc" // differs from existing.ExternalId (null)
        };

        var request = new AnalyzeImportRequest
        {
            Source = "akahu",
            AccountId = accountId,
            UserId = userId,
            Candidates = new List<ImportCandidateDto> { candidate },
            Options = new ImportAnalysisOptions
            {
                DateToleranceDays = 0, // exact match, as the bank-sync path configures
                AmountTolerance = 0m
            }
        };

        var act = async () => await CreateService().AnalyzeImportAsync(request);

        var result = await act.Should().NotThrowAsync();

        var reviewItem = result.Subject.ReviewItems.Should().ContainSingle().Subject;
        var conflict = reviewItem.Conflicts.Should().ContainSingle().Subject;
        conflict.Type.Should().Be(ConflictType.PotentialDuplicate);
        conflict.ConfidenceScore.Should().BeInRange(0m, 1m, "confidence must be a real number, not NaN/overflow");
    }
}
