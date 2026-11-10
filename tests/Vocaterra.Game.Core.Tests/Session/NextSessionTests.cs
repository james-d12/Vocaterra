using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// When the player sleeps (L01, D-game-25, design 9.5): day 1 keeps its ending (the objective sends the player to the fire and the arrow goes there); from day 2 the fire offers
/// the sleep whenever the sleep drive is below 75 and says «No tengo sueño.» otherwise; no lesson, class or order holds it back; a playtest offers none.
/// </summary>
public sealed class NextSessionTests
{
    private static string Hud(VoxelSession session) => string.Concat(session.ObjectiveHud().Select(static p => p.Text));

    private static void Sleep(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session, TestGame.Route()));

    // Stands the player on the ground three blocks south of their fire ring (where the scripted r20-sleep stands), as --teleport does.
    private static void TeleportToFire(VoxelSession session)
    {
        var ring = session.Scene.Point(VoxelSession.PlayerFireRing);
        var grid = session.Scene.Grid;
        var (x, z) = (ring.X, ring.Z + 3);
        var y = Enumerable.Range(1, grid.SizeY - 3).Reverse().First(y => grid.IsSolid(x, y, z) && !BlockDefinition.IsLeaves(grid.GetBlock(x, y, z)) && !grid.IsSolid(x, y + 1, z));
        session.Simulation.TeleportTo(new BlockPos(x, y + 1, z));
    }

    [Fact]
    public void AfterTheFirstDaysEndingTheObjectiveSaysToSleepAtTheFireAndAPlaytestKeepsItsLine()
    {
        var session = SleepTests.EndedFirstSession();
        TeleportToFire(session);

        Assert.Null(session.CurrentObjective);
        Assert.True(session.SleepWanted);
        Assert.True(session.RestDue);
        var line = Assert.Single(session.ObjectiveHud());
        Assert.Equal("Session complete: sleep at your fire to start session 2", line.Text);
        Assert.Equal(ObjectivePieceKind.Support, line.Kind);
        Assert.True(line.Done);
        Assert.True(session.AtSleepPlace);
        Assert.True(session.CanSleep);
        Assert.True(SleepOffer.AtFire(session, free: true));
        Assert.False(SleepOffer.AtFire(session, free: false)); // a panel, a screen or an NPC under the crosshair comes first
        Assert.Equal("E: sleep until morning (session 2)", SleepOffer.PromptText(session));
        Assert.Equal("Enter: sleep until morning and start session 2", SleepOffer.CardText(session));
        Assert.True(SleepOffer.OnCard(session, cardUp: true, free: true));
        Assert.False(SleepOffer.OnCard(session, cardUp: false, free: true));

        session.SleepOffered = false; // a playtest: one session per run
        Assert.Equal("Session complete: your fire is lit", Hud(session));
        Assert.False(session.CanSleep);
        Assert.False(SleepOffer.AtFire(session, free: true));
        Assert.False(SleepOffer.OnCard(session, cardUp: true, free: true));
    }

    [Fact]
    public void AwayFromTheFireThePromptIsNotShownAndTheMorningNamesNoLesson()
    {
        var session = SleepTests.EndedFirstSession();
        Stage.TeleportToNpc(session, "guide");

        Assert.False(session.AtSleepPlace);
        Assert.False(SleepOffer.AtFire(session, free: true));
        Assert.False(SleepOffer.OnCard(session, cardUp: true, free: true)); // Y01: away from a usable sleep place the card offers no sleep (the arrow says where to go)

        Sleep(session);

        Assert.Equal(2, session.Day);
        Assert.NotEqual("villager", session.Pointer?.Point);
        Assert.Equal("Good morning: session 2 begins", SleepOffer.MorningText(session));
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(90, false)]
    [InlineData(75, false)]
    [InlineData(74, true)]
    [InlineData(60, true)]
    [InlineData(0, true)]
    public void FromDayTwoTheFireOffersSleepWhenTheSleepDriveIsBelow75AndSaysNoTengoSuenoOtherwise(double sleep, bool tired)
    {
        var session = SleepTests.EndedFirstSession();
        TeleportToFire(session);
        Sleep(session);
        session.SetNeed(NeedMeter.Sleep, sleep);

        Assert.True(session.AtSleepPlace);
        Assert.True(session.CanSleep); // nothing holds it back: the drive alone says whether the player wants it
        Assert.Equal(tired, session.SleepWanted);
        Assert.Equal(tired, SleepOffer.AtFire(session, free: true));
        Assert.Equal(!tired, SleepOffer.AtFireNotTired(session, free: true));
        Assert.Equal("“No tengo sueño.” (I'm not sleepy.)", SleepOffer.NotSleepyText(session));
    }

    [Fact]
    public void NoClassNoOrderAndNoLessonHoldsTheSleepBack()
    {
        var session = SleepTests.EndedFirstSession();
        TeleportToFire(session);
        Sleep(session);
        session.SetNeed(NeedMeter.Sleep, 40);
        session.OpenTalk("villager");
        Assert.True(session.Lessons.Start("personas")); // a class taken, then left with Esc: paused
        session.EndTalk();

        Assert.Equal("personas", session.Lessons.Active?.Id);
        Assert.False(session.Lessons.Engaged);
        Assert.True(session.CanSleep);
        Assert.True(SleepOffer.AtFire(session, free: true));
        Assert.True(session.Lessons.IsUnlocked(session.Lessons.Book.Find("personas")!));
        Assert.Equal(LessonStatus.InProgress, session.Lessons.StatusOf("personas"));
    }

    [Fact]
    public void WithTheNeedsOffTheDriveStaysFullButTheScriptsAidsCanSleep()
    {
        var session = SleepTests.EndedFirstSession();
        TeleportToFire(session);
        Sleep(session);
        session.NeedsEnabled = false;

        Assert.False(session.SleepWanted); // the player is never tired
        Assert.True(session.CanSleep); // --sleep and the scripted sleep step ask CanSleep alone
    }
}
