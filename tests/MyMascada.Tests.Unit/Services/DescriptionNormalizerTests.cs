using MyMascada.Application.Features.Categorization.Services;

namespace MyMascada.Tests.Unit.Services;

public class DescriptionNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void Normalize_EmptyOrNull_ReturnsEmpty(string? input, string expected)
    {
        DescriptionNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Lowercase()
    {
        DescriptionNormalizer.Normalize("PAK N SAVE PETONE").Should().Be("pak n save petone");
    }

    [Theory]
    [InlineData("Purchase 15/03/2026", "purchase")]
    [InlineData("Purchase 2026-03-15", "purchase")]
    [InlineData("Purchase 15-03-2026", "purchase")]
    [InlineData("Purchase 03/15/2026", "purchase")]
    [InlineData("Purchase 15.03.2026", "purchase")]
    public void Normalize_RemovesDatePatterns(string input, string expected)
    {
        DescriptionNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("PAYMENT #12345", "payment")]
    [InlineData("PAYMENT REF:12345", "payment")]
    [InlineData("PAYMENT REF-99999", "payment")]
    [InlineData("PAYMENT #REF-12345", "payment")]
    public void Normalize_RemovesReferenceNumbers(string input, string expected)
    {
        DescriptionNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_PreservesRefInsideWords()
    {
        // "REFRESH" should not be stripped — REF is part of a word, not a standalone ref
        DescriptionNormalizer.Normalize("REFRESH SUBSCRIPTION").Should().Be("refresh subscription");
    }

    [Fact]
    public void Normalize_PreservesUnicodeLetters()
    {
        DescriptionNormalizer.Normalize("CAFÉ MÜNCHEN").Should().Be("café münchen");
    }

    [Fact]
    public void Normalize_RemovesTrailingNumbers()
    {
        DescriptionNormalizer.Normalize("NETFLIX.COM   12345").Should().Be("netflix com");
    }

    [Fact]
    public void Normalize_RemovesSpecialChars_KeepsHyphensAndSpaces()
    {
        // #1 is stripped by RefPattern (hash+digits), apostrophes and @ are special chars
        DescriptionNormalizer.Normalize("PAK'N'SAVE @PETONE #1").Should().Be("pak n save petone");
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        DescriptionNormalizer.Normalize("  PAK   N   SAVE  ").Should().Be("pak n save");
    }

    [Fact]
    public void Normalize_FullExample()
    {
        var result = DescriptionNormalizer.Normalize("PAK N SAVE PETONE NZ 15/03/2026 #REF-12345");
        result.Should().Be("pak n save petone nz");
    }

    [Fact]
    public void Normalize_NetflixExample()
    {
        var result = DescriptionNormalizer.Normalize("NETFLIX.COM  800-123-4567");
        // Phone numbers with hyphens are kept as individual segments after special char removal
        result.Should().Be("netflix com 800 123 4567");
    }

    [Fact]
    public void ExtractTokens_ReturnsSignificantTokens()
    {
        var tokens = DescriptionNormalizer.ExtractTokens("pak n save petone");
        tokens.Should().Contain("save");
        tokens.Should().Contain("petone");
        tokens.Should().NotContain("pak");  // 3 chars, not > 3
        tokens.Should().NotContain("n");    // 1 char
    }

    [Fact]
    public void ExtractTokens_ExcludesStopWords()
    {
        var tokens = DescriptionNormalizer.ExtractTokens("purchase from the store");
        tokens.Should().Contain("purchase");
        tokens.Should().Contain("store");
        tokens.Should().NotContain("from");
        tokens.Should().NotContain("the");
    }

    [Fact]
    public void ExtractTokens_EmptyInput_ReturnsEmpty()
    {
        DescriptionNormalizer.ExtractTokens("").Should().BeEmpty();
        DescriptionNormalizer.ExtractTokens(null!).Should().BeEmpty();
    }

    [Fact]
    public void ExtractTokens_NoDuplicates()
    {
        var tokens = DescriptionNormalizer.ExtractTokens("store store store");
        tokens.Should().HaveCount(1);
    }
}
