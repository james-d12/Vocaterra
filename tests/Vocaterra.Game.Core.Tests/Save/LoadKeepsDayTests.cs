using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>
/// A load keeps its day (V25): the day of the village, which lessons, objectives, orders, unlocks and the check battery follow, is restored as saved and only a sleep moves it;
/// the sitting, which the phrase-credit spacing rule reads, goes up on each load and each sleep. A save from before V25 has no sitting, and its session is both.
/// </summary>
public sealed class LoadKeepsDayTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession LoadOf(string text, bool bump = true) =>
        SaveGame.Load(text, TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: bump);

    [Fact]
    public void ASaveInDayFiveLoadsOnDayFiveWithItsObjectiveAndTheOpenOrderIsDelivered()
    {
        var session = TestGame.NewSession().TakeOrders("oven-wood"); // the order taken on (issue #54): the save keeps it
        session.Day = 5;
        session.Sitting = 5;
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);

        var loaded = LoadOf(SaveGame.Save(session, Pristine()));
        Assert.Equal(["oven-wood"], loaded.GivenOrders);

        Assert.Equal(5, loaded.Day);
        Assert.Equal(6, loaded.Sitting);
        Assert.Equal(session.CurrentObjective!.Id, loaded.CurrentObjective!.Id);
        _ = loaded.OpenTalk("baker");
        Assert.True(loaded.HandOverOpen); // «Necesitas más monedas.» before the fix: the order had moved on to day 6; now Luz asks for the wood (D-game-27)
        _ = loaded.HandOverGoods();
        Assert.True(loaded.Objectives.Single(o => o.Id == "oven-wood").Complete);
    }

    [Fact]
    public void ASaveMadeJustAfterASleepLoadsOnTheNewDayAndNoDayIsSkipped()
    {
        var session = SleepTests.EndedFirstSession();
        SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));
        Assert.Equal(2, session.Day);

        var loaded = LoadOf(SaveGame.Save(session, Pristine()));

        Assert.Equal(2, loaded.Day);
        Assert.Equal(session.Sitting + 1, loaded.Sitting);
        Assert.Equal(session.Lessons.OpenClass()?.Id, loaded.Lessons.OpenClass()?.Id);
    }

    [Fact]
    public void ASleepMovesTheDayAndTheSittingAndALoadMovesTheSittingAlone()
    {
        var session = SleepTests.EndedFirstSession();
        var (day, sitting) = (session.Day, session.Sitting);

        var loaded = LoadOf(SaveGame.Save(session, Pristine()));
        Assert.Equal((day, sitting + 1), (loaded.Day, loaded.Sitting));
        Assert.Equal((day, sitting), (LoadOf(SaveGame.Save(session, Pristine()), bump: false).Day, session.Sitting));

        SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));
        Assert.Equal((day + 1, sitting + 1), (session.Day, session.Sitting));
    }

    [Fact]
    public void AUseInANewSittingBypassesTheSpacingGapAndAUseInTheSameSittingDoesNot()
    {
        var session = TestGame.NewSession();
        var credit = CreditRules.FullTyped;
        session.Day = 3;
        session.Sitting = 3;
        var use = new PhraseUse(60_000, 1_000, "pedir-agua", PhraseUseKind.Supported, "shopkeeper", "stall-need", "quiero agua, por favor", null, InputKind.Spoken, true, SupportLevel.None, null);
        _ = session.PhraseProgress.Apply(use, credit);
        var text = SaveGame.Save(session, Pristine());

        var sameSitting = LoadOf(text, bump: false);
        _ = sameSitting.PhraseProgress.Apply(use with { Time = 1_060 }, credit);
        Assert.Equal(1, sameSitting.PhraseProgress.RecordOf("pedir-agua").CreditedUses);

        var nextSitting = LoadOf(text);
        Assert.Equal(3, nextSitting.Day);
        _ = nextSitting.PhraseProgress.Apply(use with { Time = 1_060 }, credit);
        Assert.Equal(2, nextSitting.PhraseProgress.RecordOf("pedir-agua").CreditedUses);
        Assert.Equal(4, nextSitting.PhraseProgress.RecordOf("pedir-agua").LastCreditedSession);
    }

    [Fact]
    public void ASaveFromBeforeTheSittingLoadsOnItsSavedDayAndContinuesTheSittingFromIt()
    {
        var text = File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", "save-v4.json"));
        var document = SaveGame.Parse(text);
        Assert.Null(document.Sitting);

        var loaded = LoadOf(text);

        Assert.Equal(document.Session, loaded.Day);
        Assert.Equal(document.Session + 1, loaded.Sitting);
    }
}
