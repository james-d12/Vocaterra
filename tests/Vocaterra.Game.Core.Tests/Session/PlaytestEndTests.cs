using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The playtest's end rules (S12) without a window: the session's save file, the F6 folder, the transfer offer, the post-check's start and the end key.</summary>
public sealed class PlaytestEndTests
{
    [Fact]
    public void TheSaveFileIsPlaytestSaveInTheSessionFolder()
    {
        var folder = Path.Combine("sessions", "20261022-101530-s1");
        Assert.Equal(Path.Combine(folder, "playtest.save"), PlaytestEnd.SaveIn(folder));
        Assert.Throws<ArgumentException>(() => PlaytestEnd.SaveIn(string.Empty));
    }

    [Fact]
    public void F6WritesThePlaytestSaveInTheSessionFolderNotTheWorkingFolder()
    {
        var playtestSave = PlaytestEnd.SaveIn(Path.Combine("sessions", "s1"));

        // A playtest's F6 goes to its session folder (the build folder is the tester's working folder); --save still wins; an ordinary run keeps its own.
        Assert.Equal(playtestSave, PlaytestEnd.ManualSavePath(null, playtestSave, "vocaterra.save.json"));
        Assert.Equal("chosen.json", PlaytestEnd.ManualSavePath("chosen.json", playtestSave, "vocaterra.save.json"));
        Assert.Equal("vocaterra.save.json", PlaytestEnd.ManualSavePath(null, null, "vocaterra.save.json"));
    }

    [Fact]
    public void TheTransferIsOfferedWhenTheGuideMadeHerOffer()
    {
        var offered = new GameEvent[] { new WorldEvent(10, 1, WorldEvents.TalkOpened, "guide") };
        Assert.False(PlaytestEnd.TransferOffered(offered));

        var made = offered.Append(new WorldEvent(12, 1, WorldEvents.TransferOffered, "guide", "¿Quieres agua?"));
        Assert.True(PlaytestEnd.TransferOffered(made));
    }

    [Fact]
    public void ThePostCheckStartsByItselfOnlyWhenEveryConditionHolds()
    {
        // playtest, worldRunning, started, campDone, transferOffered, talkOpen
        Assert.True(PlaytestEnd.StartsOnItsOwn(true, true, false, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(false, true, false, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, false, false, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, true, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, false, false, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, false, true, false, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, false, true, true, true)); // a talk still open finishes first
    }

    [Fact]
    public void TheEndKeyStartsTheRunOnlyOnceInThePlaytestWorld()
    {
        Assert.True(PlaytestEnd.EndKeyStarts(true, true, false));
        Assert.Equal("F10", PlaytestEnd.EndKeyName);
        Assert.False(PlaytestEnd.EndKeyStarts(false, true, false));
        Assert.False(PlaytestEnd.EndKeyStarts(true, false, false)); // a battery or the mic check is on
        Assert.False(PlaytestEnd.EndKeyStarts(true, true, true)); // already started
    }
}
