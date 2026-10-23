using ClipboardManagerWinForm.Services;

namespace ClipboardManagerWinForm.Tests;

public sealed class TextFiltersTests
{
    [Fact]
    public void TryJoinBrowserLink_JoinsLinkSplitAcrossLines()
    {
        var result = TextFilters.TryJoinBrowserLink(
            "https://www.example.com/some/very/long\r\n/path/file.html",
            out var joined);

        Assert.True(result);
        Assert.Equal("https://www.example.com/some/very/long/path/file.html", joined);
    }

    [Fact]
    public void TryJoinBrowserLink_LeavesRegularMultilineTextUntouched()
    {
        var result = TextFilters.TryJoinBrowserLink("Hello\r\nWorld", out var joined);

        Assert.False(result);
        Assert.Equal("Hello\r\nWorld", joined);
    }

    [Fact]
    public void TryJoinBrowserLink_RequiresAtLeastTwoLines()
    {
        var result = TextFilters.TryJoinBrowserLink("https://example.com", out _);

        Assert.False(result);
    }

    [Fact]
    public void ContainsIgnoredText_FindsConfiguredPatterns()
    {
        Assert.True(TextFilters.ContainsIgnoredText("my secret value", ";secret;password", true));
        Assert.False(TextFilters.ContainsIgnoredText("my secret value", ";secret;password", false));
        Assert.False(TextFilters.ContainsIgnoredText("public value", ";secret;password", true));
    }

    [Fact]
    public void SplitPatterns_TrimsAndSkipsEmptyEntries()
    {
        Assert.Equal(new[] { "foo", "bar" }, TextFilters.SplitPatterns(";foo;; bar ;"));
    }

    [Fact]
    public void CreateTooltip_CollapsesWhitespaceAndLimitsLength()
    {
        var tooltip = TextFilters.CreateTooltip("first\r\nsecond\tthird", 12);

        Assert.Equal("first second", tooltip);
        Assert.True(tooltip.Length <= 12);
    }

    [Fact]
    public void Summarize_TruncatesWithEllipsis()
    {
        var summary = TextFilters.Summarize("0123456789", 5);

        Assert.Equal("0123…", summary);
    }

    [Fact]
    public void ToPlainText_ReplacesNoBreakSpacesWithSpaces()
    {
        var plain = TextFilters.ToPlainText("two\u00A0words\u2007and\u202Fmore");

        Assert.Equal("two words and more", plain);
    }

    [Fact]
    public void ToPlainText_DropsInvisibleCharacters()
    {
        var plain = TextFilters.ToPlainText("in\u200Bvis\u200Cible\u200D text\uFEFF and soft\u00ADhyphen");

        Assert.Equal("invisible text and softhyphen", plain);
    }

    [Fact]
    public void ToPlainText_NormalizesLineBreaksAndKeepsTabs()
    {
        var plain = TextFilters.ToPlainText("first\r\nsecond\rthird\nfourth");

        Assert.Equal("first\r\nsecond\r\nthird\r\nfourth", plain);
    }

    [Fact]
    public void ToPlainText_KeepsTabsAndDropsOtherControlCharacters()
    {
        var plain = TextFilters.ToPlainText("column\u0009two\u000Cpage\u0000end");

        Assert.Equal("column\ttwopageend", plain);
    }

    [Fact]
    public void ToPlainText_ReturnsEmptyForMissingText()
    {
        Assert.Equal(string.Empty, TextFilters.ToPlainText(null));
        Assert.Equal(string.Empty, TextFilters.ToPlainText(string.Empty));
    }
}
