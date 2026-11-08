using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Menu;

/// <summary>The controls page is made from the control table, so it names exactly the keys the game uses.</summary>
public sealed class ControlsListingTests
{
    private static IReadOnlyList<ControlsRow> Rows() => ControlsListing.Rows(TestGame.Locale.Ui, TestGame.Locale.KeyName);

    [Fact]
    public void Given_TheTable_When_TheRowsAreMade_Then_EveryControlAppearsWithItsKeysAndItsDescription()
    {
        var rows = Rows();
        foreach (var entry in ControlTable.All)
        {
            var description = TestGame.Locale.Ui("control." + entry.Id);
            var row = Assert.Single(rows, r => !r.IsHeading && r.Text == description && r.Keys == ControlsListing.KeysText(entry, TestGame.Locale.Ui, TestGame.Locale.KeyName));
            Assert.NotEmpty(row.Keys);
        }

        Assert.Equal(Enum.GetValues<ControlGroup>().Length, rows.Count(static r => r.IsHeading));
    }

    [Fact]
    public void Given_TheKeysOfAControl_When_Spelled_Then_TheyAreTheTablesKeysInTheLocalesWords()
    {
        var locale = TestGame.Locale;
        string Spell(string id) => ControlsListing.KeysText(ControlTable.Entry(id), locale.Ui, locale.KeyName);
        Assert.Equal("W or Up", Spell("walkForward"));
        Assert.Equal("Esc", Spell("menu"));
        Assert.Equal("Left Shift", Spell("sprint"));
        Assert.Equal("1 to 0 or Mouse wheel", Spell("hotbar"));
        Assert.Equal("Mouse", Spell("look"));
        Assert.Equal("Left mouse button", Spell("gather"));
    }

    [Fact]
    public void Given_ARebindOfAnEntry_When_Spelled_Then_TheScreenFollowsTheEntryNotAHandWrittenCopy()
    {
        var moved = new ControlEntry("x", ControlGroup.General, [GameKey.Q, GameKey.Z]);
        Assert.Equal("Q or Z", ControlsListing.KeysText(moved, TestGame.Locale.Ui, TestGame.Locale.KeyName));
    }

    [Fact]
    public void Given_TheKeysTheGameReads_When_ComparedToTheTable_Then_NoneIsMissingFromIt()
    {
        // The dialogue panel, the lesson strip, the book and the station read their keys from the table: a key pressed there is a key listed.
        var listed = ControlTable.All.SelectMany(static e => e.Keys).ToHashSet();
        foreach (var name in new[] { "repeat", "slower", "help", "dontKnow", "lessonStart", "lessonSkip", "lessonMore", "bookPractise", "stationBake", "stationHelp" })
        {
            Assert.Contains(ControlTable.Key(name), listed);
        }
    }
}
