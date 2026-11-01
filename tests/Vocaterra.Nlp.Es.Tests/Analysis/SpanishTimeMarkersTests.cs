namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Es.Analysis;

/// <summary>
/// M64 (e), the review of <c>feature/village</c> (its cleanups): <see cref="SpanishTimeMarkers"/> exposed collection expressions as <c>IReadOnlyList&lt;string&gt;</c>, and
/// the review feared a caller could cast one to an array or a list and change the table the checker and the fuzz suite read. Each list is now a
/// <c>ReadOnlyCollection</c> (<c>Array.AsReadOnly</c>); the property types are as they were. These tests passed before M64 as well: the compiler's own type for a
/// collection expression of <c>IReadOnlyList&lt;T&gt;</c> is a read-only wrapper, so the change makes explicit what held, and these tests pin it whatever type the
/// compiler chooses.
/// </summary>
public class SpanishTimeMarkersTests
{
    public static TheoryData<string, IReadOnlyList<string>> Lists => new()
    {
        { nameof(SpanishTimeMarkers.PastAdverbs), SpanishTimeMarkers.PastAdverbs },
        { nameof(SpanishTimeMarkers.PastAdjectiveForms), SpanishTimeMarkers.PastAdjectiveForms },
        { nameof(SpanishTimeMarkers.WordsBeforeTheNounMañana), SpanishTimeMarkers.WordsBeforeTheNounMañana },
        { nameof(SpanishTimeMarkers.DayAfterTomorrow), SpanishTimeMarkers.DayAfterTomorrow },
        { nameof(SpanishTimeMarkers.UpcomingClause), SpanishTimeMarkers.UpcomingClause },
        { nameof(SpanishTimeMarkers.TimeNouns), SpanishTimeMarkers.TimeNouns },
        { nameof(SpanishTimeMarkers.UpcomingTimeNouns), SpanishTimeMarkers.UpcomingTimeNouns },
    };

    [Theory]
    [MemberData(nameof(Lists))]
    public void AListCannotBeCastAndChanged(string name, IReadOnlyList<string> list)
    {
        Assert.False(list is string[], name);
        var asList = Assert.IsAssignableFrom<IList<string>>(list);
        Assert.True(asList.IsReadOnly, name);
        Assert.Throws<NotSupportedException>(() => asList[0] = "changed");
        Assert.NotEqual("changed", list[0]);
    }

    [Fact]
    public void TheListsKeepTheirWords()
    {
        Assert.Equal(["ayer", "anoche", "anteayer"], SpanishTimeMarkers.PastAdverbs);
        Assert.Equal(["pasado", SpanishTimeMarkers.FutureAdverb], SpanishTimeMarkers.DayAfterTomorrow);
        Assert.Equal(SpanishTimeMarkers.TimeNouns, SpanishTimeMarkers.UpcomingTimeNouns.Take(SpanishTimeMarkers.TimeNouns.Count));
    }
}
