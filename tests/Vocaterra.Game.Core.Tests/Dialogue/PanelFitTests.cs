using Vocaterra.Game.Core.Dialogue;

namespace Vocaterra.Game.Core.Tests.Dialogue;

/// <summary>U02b, the dialogue panel's fit: every older line reachable on the page, the rows that say what is out of view, and the collapsed exchange kept at small sizes.</summary>
public sealed class PanelFitTests
{
    private static PanelBlock Line(int index, int rows = 1, int english = 0, DialogueSpeaker speaker = DialogueSpeaker.Guide) => new(index, speaker, rows, english);

    [Fact]
    public void TheNewestPageFitsTheNewestLinesAndSaysHowManyEarlierLinesAreAbove()
    {
        var blocks = Enumerable.Range(0, 8).Select(i => Line(i)).ToList();
        var fit = PanelFitter.Fit(blocks, capacity: 4, expanded: true, scrollBack: 0, hiddenBefore: 0);
        Assert.Equal(5, fit.First); // the hidden-lines row takes the room of one line
        Assert.Equal(8, fit.End);
        Assert.True(fit.ShowEarlier);
        Assert.Equal(5, fit.EarlierLines);
        Assert.False(fit.ShowNewer);
    }

    [Fact]
    public void ScrolledToTheTopTheOldestLineShowsWithNoEarlierRowAndTheNewerRowCountsWhatIsBelow()
    {
        var blocks = Enumerable.Range(0, 8).Select(i => Line(i)).ToList();
        var fit = PanelFitter.Fit(blocks, capacity: 4, expanded: true, scrollBack: 7, hiddenBefore: 0);
        Assert.Equal(0, fit.First);
        Assert.True(fit.Shows(0));
        Assert.False(fit.ShowEarlier);
        Assert.True(fit.ShowNewer);
        Assert.Equal(7, fit.NewerLines);
    }

    [Fact]
    public void APageThatFitsWholeShowsEveryLineAndNoScrollRows()
    {
        var blocks = Enumerable.Range(0, 3).Select(i => Line(i, rows: 2, english: 1)).ToList();
        var fit = PanelFitter.Fit(blocks, capacity: 9, expanded: true, scrollBack: 0, hiddenBefore: 0);
        Assert.Equal(0, fit.First);
        Assert.Equal(3, fit.End);
        Assert.False(fit.ShowEarlier);
        Assert.All(fit.ShowEnglish, shown => Assert.True(shown));
    }

    [Fact]
    public void TheEarlierRowTakesARowOfTheRoom()
    {
        var blocks = Enumerable.Range(0, 4).Select(i => Line(i)).ToList();
        var fit = PanelFitter.Fit(blocks, capacity: 3, expanded: true, scrollBack: 0, hiddenBefore: 0);
        Assert.Equal(2, fit.First);
        Assert.Equal(2, fit.EarlierLines);
    }

    [Fact]
    public void TheCollapsedExchangeDropsItsEnglishBeforeTheCoachingLine()
    {
        var blocks = new List<PanelBlock>
        {
            Line(5, rows: 1, speaker: DialogueSpeaker.Player),
            Line(6, rows: 2, english: 1),
            new(-1, DialogueSpeaker.Coaching, 2, 0),
        };

        var roomy = PanelFitter.Fit(blocks, capacity: 6, expanded: false, scrollBack: 0, hiddenBefore: 3);
        Assert.True(roomy.ShowEnglish[1]);
        Assert.True(roomy.Shows(2));
        Assert.False(roomy.ShowEarlier);

        var tighter = PanelFitter.Fit(blocks, capacity: 5, expanded: false, scrollBack: 0, hiddenBefore: 3);
        Assert.False(tighter.ShowEnglish[1]);
        Assert.True(tighter.Shows(2));
        Assert.True(tighter.Shows(0) && tighter.Shows(1));

        var tight = PanelFitter.Fit(blocks, capacity: 4, expanded: false, scrollBack: 0, hiddenBefore: 3);
        Assert.False(tight.Shows(2)); // the coaching line goes before the learner's line or the NPC's Spanish
        Assert.True(tight.Shows(0) && tight.Shows(1));
        Assert.True(tight.ShowEnglish[1]); // the coaching line went first; the English under the reply fits in its place
    }

    [Fact]
    public void TheCollapsedHiddenLinesRowGoesBeforeTheExchangeAndSaysHowManyLinesAreHidden()
    {
        var blocks = new List<PanelBlock>
        {
            Line(5, rows: 1, speaker: DialogueSpeaker.Player),
            Line(6, rows: 1),
        };

        var fit = PanelFitter.Fit(blocks, capacity: 2, expanded: false, scrollBack: 0, hiddenBefore: 4);
        Assert.True(fit.Shows(0) && fit.Shows(1));
        Assert.False(fit.ShowEarlier);
        Assert.Equal(4, fit.EarlierLines);

        var roomy = PanelFitter.Fit(blocks, capacity: 3, expanded: false, scrollBack: 0, hiddenBefore: 4);
        Assert.True(roomy.ShowEarlier);
        Assert.Equal(4, roomy.EarlierLines);
    }

    [Fact]
    public void AnEmptyPanelShowsNothingButItsOlderLinesCount()
    {
        var fit = PanelFitter.Fit([], capacity: 3, expanded: false, scrollBack: 0, hiddenBefore: 2);
        Assert.Equal(0, fit.First);
        Assert.Equal(0, fit.End);
        Assert.True(fit.ShowEarlier);
        Assert.Equal(2, fit.EarlierLines);
    }

    // U02b: the developer note between the learner's line and the reply goes before the learner's line does (800x600 at 150 %, run with --dev).
    [Fact]
    public void ADeveloperNoteIsDroppedBeforeTheLearnersLineAndKeptWhenThereIsRoom()
    {
        var blocks = new List<PanelBlock>
        {
            Line(5, rows: 1, speaker: DialogueSpeaker.Player),
            Line(6, rows: 1, speaker: DialogueSpeaker.Note),
            Line(7, rows: 1),
        };

        var tight = PanelFitter.Fit(blocks, capacity: 2, expanded: false, scrollBack: 0, hiddenBefore: 0);
        Assert.True(tight.Shows(0));
        Assert.False(tight.Shows(1));
        Assert.True(tight.Shows(2));

        var roomy = PanelFitter.Fit(blocks, capacity: 3, expanded: false, scrollBack: 0, hiddenBefore: 0);
        Assert.True(roomy.Shows(0) && roomy.Shows(1) && roomy.Shows(2));
    }
}
