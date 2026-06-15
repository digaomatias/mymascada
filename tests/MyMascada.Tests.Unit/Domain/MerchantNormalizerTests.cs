using MyMascada.Domain.Common;

namespace MyMascada.Tests.Unit.Domain;

public class MerchantNormalizerTests
{
    #region Embedded reference-number stripping

    [Fact]
    public void Normalize_ShouldStripEmbeddedNumericReference_SoSameMerchantNormalizesEqual()
    {
        // Two NZ-style bank feed descriptions for the same merchant that differ only by an
        // embedded reference number. These must collapse to the same normalized key.
        var a = MerchantNormalizer.Normalize("AMI INSURANCE AMIINSURANCE 3301234");
        var b = MerchantNormalizer.Normalize("AMI INSURANCE AMIINSURANCE 3305678");

        a.Should().Be(b);
        a.Should().Be("ami insurance");
    }

    [Theory]
    [InlineData("AMI INSURANCE AMIINSURANCE 3301234", "ami insurance")]
    [InlineData("SPOTIFY 9928374 SUBSCRIPTION", "spotify subscription")]
    [InlineData("POWER CO 0012849 AUTOPAY", "power co autopay")]
    public void Normalize_ShouldStripStandaloneNumericTokens(string input, string expected)
    {
        MerchantNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldCollapseDoubledMerchantWordArtifact()
    {
        MerchantNormalizer.Normalize("AMI INSURANCE AMIINSURANCE").Should().Be("ami insurance");
    }

    [Fact]
    public void Normalize_ShouldStripLongMostlyNumericAlphanumericToken()
    {
        // Reference token that is mostly digits should be removed.
        MerchantNormalizer.Normalize("MERCHANT INV00123AB").Should().Be("merchant");
    }

    [Fact]
    public void Normalize_ShouldKeepShortNumberThatIsPartOfBrand()
    {
        // A short numeric token (< 3 digits) that is part of the brand should survive.
        MerchantNormalizer.Normalize("3 MOBILE").Should().Be("3 mobile");
    }

    [Theory]
    [InlineData("MERCHANT #12345", "merchant")]
    [InlineData("ACME CORP REF:ABC123", "acme corp")]
    [InlineData("STORE ID:987654", "store")]
    public void Normalize_ShouldStripExplicitReferencePrefixes(string input, string expected)
    {
        MerchantNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("RAPID RENTALS", "rapid rentals")]   // "id" inside "rapid" must survive
    [InlineData("VIDEO STORE", "video store")]       // "id" inside "video" must survive
    [InlineData("REFRESH CAFE", "refresh cafe")]     // "ref" inside "refresh" must survive
    public void Normalize_ShouldNotStripReferenceTokensInsideWords(string input, string expected)
    {
        MerchantNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldNotCollapseGenuinelyDifferentMerchants()
    {
        var netflix = MerchantNormalizer.Normalize("NETFLIX 12345");
        var spotify = MerchantNormalizer.Normalize("SPOTIFY 67890");

        netflix.Should().NotBe(spotify);
        netflix.Should().Be("netflix");
        spotify.Should().Be("spotify");
    }

    #endregion

    #region Transitive merge (union-find connected components)

    [Fact]
    public void GroupSimilarKeys_ShouldTransitivelyMerge_WhenAandCNotDirectlySimilar()
    {
        // Build an explicit chain where the endpoints are NOT directly similar but each
        // is similar to a shared middle key.
        var keys = new[]
        {
            "netflixaaaa",   // A
            "netflixaabb",   // B: 2 diff from A
            "netflixbbbb"    // C: 2 diff from B, 4 diff from A
        };

        // A vs C differ by 4/11 chars -> ~0.64 similarity (below 0.8, not directly similar).
        // A vs B and B vs C differ by 2/11 -> ~0.82 (above 0.8). Union-find must chain them.
        MerchantNormalizer.CalculateSimilarity(keys[0], keys[2])
            .Should().BeLessThan(0.8m, "endpoints are not directly similar");
        MerchantNormalizer.CalculateSimilarity(keys[0], keys[1])
            .Should().BeGreaterThan(0.8m);
        MerchantNormalizer.CalculateSimilarity(keys[1], keys[2])
            .Should().BeGreaterThan(0.8m);

        var mapping = MerchantNormalizer.GroupSimilarKeys(keys);

        mapping.Values.Distinct().Should().HaveCount(1,
            "transitive merge should collapse A~B~C even though A is not similar to C");
    }

    [Fact]
    public void GroupSimilarKeys_ShouldKeepDistinctMerchantsSeparate()
    {
        var keys = new[] { "netflix", "spotify", "power company" };

        var mapping = MerchantNormalizer.GroupSimilarKeys(keys);

        mapping.Values.Distinct().Should().HaveCount(3);
    }

    #endregion
}
