using FluentAssertions;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Models.Configuration;
using MergeUtility.Core.Services;

namespace MergeUtility.Tests;

public class IdentifierExtractorTests
{
    private static RegexIdentifierExtractor CreateExtractor(string pattern = @"^(.+?)_LF\d+")
    {
        var options = Options.Create(new MergeOptions { IdentifierRegex = pattern });
        return new RegexIdentifierExtractor(options);
    }

    [Fact]
    public void Extract_StandardKeyWithHyphen_ReturnsKey()
    {
        var extractor = CreateExtractor();
        extractor.Extract("01-11234_LF001.pdf").Should().Be("01-11234");
    }

    [Fact]
    public void Extract_AlphanumericKey_ReturnsKey()
    {
        var extractor = CreateExtractor();
        extractor.Extract("ABC123_LF002.pdf").Should().Be("ABC123");
    }

    [Fact]
    public void Extract_NoLfSuffix_ReturnsNull()
    {
        var extractor = CreateExtractor();
        extractor.Extract("badfile.pdf").Should().BeNull();
    }

    [Fact]
    public void Extract_LeadingUnderscore_ReturnsNull()
    {
        // "_LF001.pdf" → group 1 = "" → returns null per contract
        var extractor = CreateExtractor();
        extractor.Extract("_LF001.pdf").Should().BeNull();
    }

    [Fact]
    public void Extract_LowercaseLfSuffix_ReturnsKey()
    {
        // IgnoreCase is set so _lf001 should match too
        var extractor = CreateExtractor();
        extractor.Extract("ABC123_lf001.pdf").Should().Be("ABC123");
    }

    [Fact]
    public void Extract_MultiDigitSequence_ReturnsKey()
    {
        var extractor = CreateExtractor();
        extractor.Extract("DOC-2024-001_LF099.pdf").Should().Be("DOC-2024-001");
    }

    [Fact]
    public void Extract_FileNameWithNoExtension_ReturnsKey()
    {
        var extractor = CreateExtractor();
        extractor.Extract("MYKEY_LF001").Should().Be("MYKEY");
    }

    [Theory]
    [InlineData("A_LF1.pdf", "A")]
    [InlineData("XYZ_LF10.pdf", "XYZ")]
    [InlineData("long-key-value_LF999.pdf", "long-key-value")]
    public void Extract_VariousValidInputs_ReturnsExpectedKey(string input, string expected)
    {
        var extractor = CreateExtractor();
        extractor.Extract(input).Should().Be(expected);
    }
}
