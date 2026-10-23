using ClipboardManagerWinForm.Data;

namespace ClipboardManagerWinForm.Tests;

public sealed class ArchiveSearchQueryTests
{
    [Fact]
    public void Parse_WithEmptyText_ReturnsEmptyQuery()
    {
        var query = ArchiveSearchQuery.Parse("   ");

        Assert.Empty(query.Terms);
        Assert.Empty(query.ExcludedTerms);
        Assert.True(query.IsEmpty);
    }

    [Fact]
    public void Parse_SplitsTermsAndExclusions()
    {
        var query = ArchiveSearchQuery.Parse("foo -bar   baz");

        Assert.Equal(new[] { "foo", "baz" }, query.Terms);
        Assert.Equal(new[] { "bar" }, query.ExcludedTerms);
    }

    [Fact]
    public void Parse_KeepsQuotedPhraseTogether()
    {
        var query = ArchiveSearchQuery.Parse("\"hello world\" -secret");

        Assert.Equal(new[] { "hello world" }, query.Terms);
        Assert.Equal(new[] { "secret" }, query.ExcludedTerms);
    }
}
