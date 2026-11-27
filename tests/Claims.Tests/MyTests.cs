namespace Claims.Tests;

// MYT-3108: each test below passes on the fixed code and fails on the agent's version.
public class MyTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());
    private static long[] Ids(IEnumerable<Claim> claims) => claims.Select(c => c.Id).OrderBy(id => id).ToArray();

    [Fact]
    public void Search_PartialText_AnyCase_FindsDescriptionMatches()
        => Assert.Equal(new long[] { 101, 103 }, Ids(Repo().Search(1, "dental")));

    [Fact]
    public void Search_NameWithApostrophe_FindsProvider()
        => Assert.Equal(new long[] { 102 }, Ids(Repo().Search(1, "O'Brien")));

    [Fact]
    public void Search_PercentSign_IsMatchedLiterally()
        => Assert.Equal(new long[] { 104 }, Ids(Repo().Search(1, "100%")));

    [Theory]
    [InlineData("submitted_at")]
    [InlineData("foo")]
    [InlineData("amount, (SELECT name FROM members)")]
    [InlineData("amount;DROP TABLE claims")]
    [InlineData("amount -- ")]
    [InlineData("(CASE WHEN (SELECT amount FROM claims WHERE id=201)>500 THEN amount ELSE -amount END)")]
    public void Search_SortByOutsideAllowList_IsRejected(string sortBy)
        => Assert.Throws<ArgumentException>(() => Repo().Search(1, "", sortBy, "asc"));

    [Fact]
    public void Search_UnknownSortDirection_IsRejected()
        => Assert.Throws<ArgumentException>(() => Repo().Search(1, "", "amount", "sideways"));
}
