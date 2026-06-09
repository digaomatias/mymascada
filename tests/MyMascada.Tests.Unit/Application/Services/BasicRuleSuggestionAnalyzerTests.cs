using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;
using NSubstitute;

namespace MyMascada.Tests.Unit.Application.Services;

/// <summary>
/// Scenario tests for the merchant-centric rule suggestion analyzer, reproducing the real-world
/// problems observed in production: rules keyed off the cardholder name, bare single-word patterns,
/// and duplicate suggestions for the same merchant across spelling variants.
/// </summary>
public class BasicRuleSuggestionAnalyzerTests
{
    private readonly BasicRuleSuggestionAnalyzer _analyzer;
    private int _idCounter = 1;

    public BasicRuleSuggestionAnalyzerTests()
    {
        // The analyzer reads categories from the input, so the repository is unused.
        _analyzer = new BasicRuleSuggestionAnalyzer(Substitute.For<ICategoryRepository>());
    }

    private static readonly Category Groceries = new() { Id = 1, Name = "Groceries" };
    private static readonly Category Lending = new() { Id = 2, Name = "Lending Services" };
    private static readonly Category Travel = new() { Id = 3, Name = "Travel" };

    private Transaction Tx(string description, decimal amount, int? categoryId)
        => new()
        {
            Id = _idCounter++,
            Description = description,
            Amount = amount,
            TransactionDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            AccountId = 1,
            CategoryId = categoryId
        };

    private RuleAnalysisInput BuildInput(List<Transaction> transactions)
        => new()
        {
            UserId = Guid.NewGuid(),
            Transactions = transactions,
            AvailableCategories = new List<Category> { Groceries, Lending, Travel },
            ExistingRules = new List<CategorizationRule>(),
            MaxSuggestions = 10,
            MinConfidenceThreshold = 0.7,
            AccountHolderNameTokens = new List<string> { "MATIAS", "LEOTE" }
        };

    /// <summary>
    /// Builds a representative dataset: Pak'nSave groceries in two spellings, Flight Centre travel,
    /// credit-card repayment lines that contain only the cardholder name + card/number noise, and
    /// unrelated filler so coverage fractions are realistic.
    /// </summary>
    private List<Transaction> RepresentativeDataset()
    {
        var transactions = new List<Transaction>();

        // Pak'nSave — spelling A (space separated, with a repeated location suffix)
        for (var i = 0; i < 4; i++)
            transactions.Add(Tx("PAK N SAVE GLEN INNES GLEN INNES", -82.50m, Groceries.Id));

        // Pak'nSave — spelling B (apostrophe form). Same merchant, different literal text.
        for (var i = 0; i < 4; i++)
            transactions.Add(Tx("PAK'nSAVE", -64.20m, Groceries.Id));

        // Flight Centre — a genuine merchant token survives stripping "Mcard" + the holder name.
        for (var i = 0; i < 4; i++)
            transactions.Add(Tx("Flight Centre Mcard Flight Centr Matias Leote Id1104962764", -429.27m, Travel.Id));

        // Credit-card repayment lines: only the cardholder name + card product + reference numbers.
        for (var i = 0; i < 4; i++)
            transactions.Add(Tx("Gem Visa Matias Leote 000000601073 002025667927", -250.00m, Lending.Id));
        for (var i = 0; i < 3; i++)
            transactions.Add(Tx("Q Mastercard Q Mastercard Matias Leote 1104144207", -280.00m, Lending.Id));

        // Unrelated filler (each unique so none forms its own pattern).
        var fillers = new[]
        {
            "Spotify", "Z Energy Mt Wellington", "Mobil Ellerslie", "Vodafone NZ", "Trade Me",
            "Mercury Energy", "Watercare", "Auckland Transport", "Uber Eats", "The Warehouse",
            "Bunnings Warehouse", "Mitre 10 Mega"
        };
        foreach (var f in fillers)
            transactions.Add(Tx(f, -19.99m, null));

        return transactions;
    }

    [Fact]
    public async Task NeverSuggestsAPatternBasedOnTheCardholderName()
    {
        var input = BuildInput(RepresentativeDataset());

        var suggestions = await _analyzer.AnalyzePatternsAsync(input);

        suggestions.Should().NotContain(s => s.Pattern.Contains("MATIAS", StringComparison.OrdinalIgnoreCase));
        suggestions.Should().NotContain(s => s.Pattern.Contains("LEOTE", StringComparison.OrdinalIgnoreCase));
        // And specifically no rule should funnel the repayment lines into Lending via the name.
        suggestions.Should().NotContain(s => s.SuggestedCategoryName == "Lending Services");
    }

    [Fact]
    public async Task CollapsesMerchantSpellingVariantsIntoASingleSuggestion()
    {
        var input = BuildInput(RepresentativeDataset());

        var suggestions = await _analyzer.AnalyzePatternsAsync(input);

        // The old engine produced both "SAVE" and "NSAVE" for the same merchant. There must now be
        // exactly one Groceries suggestion covering both spellings.
        var grocerySuggestions = suggestions.Where(s => s.SuggestedCategoryName == "Groceries").ToList();
        grocerySuggestions.Should().HaveCount(1);
        grocerySuggestions[0].MatchingTransactions.Should().HaveCount(8); // 4 + 4 across both spellings
    }

    [Fact]
    public async Task PrefersAMultiWordMerchantPhraseOverABareWord()
    {
        var input = BuildInput(RepresentativeDataset());

        var suggestions = await _analyzer.AnalyzePatternsAsync(input);

        var travel = suggestions.SingleOrDefault(s => s.SuggestedCategoryName == "Travel");
        travel.Should().NotBeNull();
        travel!.Pattern.Should().Be("FLIGHT CENTRE");
    }

    [Fact]
    public async Task RejectsGenericTokensThatLeakAcrossManyCategories()
    {
        // "WAREHOUSE" appears in two filler rows under different categories — it must not survive as
        // a confident pattern.
        var transactions = RepresentativeDataset();
        transactions.Add(Tx("Noel Leeming Warehouse", -120m, Travel.Id));
        transactions.Add(Tx("Smiths City Warehouse", -90m, Lending.Id));
        transactions.Add(Tx("Generic Warehouse Store", -45m, Groceries.Id));
        var input = BuildInput(transactions);

        var suggestions = await _analyzer.AnalyzePatternsAsync(input);

        suggestions.Should().NotContain(s => s.Pattern == "WAREHOUSE");
    }

    [Fact]
    public async Task ReturnsNothingWhenThereAreNoMeaningfulMerchantTokens()
    {
        // Only cardholder-name repayment lines: nothing meaningful remains after stripping.
        var transactions = new List<Transaction>();
        for (var i = 0; i < 6; i++)
            transactions.Add(Tx("Gem Visa Matias Leote 000000601073 002025667927", -250m, Lending.Id));
        var input = BuildInput(transactions);

        var suggestions = await _analyzer.AnalyzePatternsAsync(input);

        suggestions.Should().BeEmpty();
    }
}
