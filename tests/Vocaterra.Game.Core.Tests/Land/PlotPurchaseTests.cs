using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>
/// Buying the plot (Y11, D-game-32): the mayor Tomás offers the stake from play session 2 and, once a plot is marked, the sale (<c>sell_plot</c>); the exchange takes the coins, owns the plot
/// and gives the camp kit as one operation, and every refusal leaves coins, bag and land as they were. Every test drives a talk turn of the shipped bundle on the shipped scene.
/// </summary>
public sealed class PlotPurchaseTests
{
    private const string Mayor = "villager";
    private const string StakePrompt = "mayor-stake";
    private const string SalePrompt = "mayor-sell";
    private static readonly (int X, int Z) Corner = LandTestScene.NorthPlot;

    private static Scene Pristine(ulong seed = LandTestScene.Seed) => TestGame.Route(seed);

    // A new world on its second day, the player at the mayor's stand-off cell, with the coins given.
    private static VoxelSession SessionTwo(int coins = 0)
    {
        var session = TestGame.NewSessionWithShippedLessons(LandTestScene.Seed, CreditRules.FullTyped);
        SessionSkip.Apply(session, 2);
        Stage.TeleportToNpc(session, Mayor);
        if (coins > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Coin, coins));
        }

        return session;
    }

    // The same, with the plot marked (the stake's own effect, here by the launch flag's call) and the talk open on the sale.
    private static VoxelSession AtTheSale(int coins)
    {
        var session = SessionTwo(coins);
        _ = session.Simulation.GrantItems(ItemId.Stake, 1); // the stake is how the plot was marked: the player holds it
        var middle = (X: Corner.X + 5, Z: Corner.Z + 5);
        Assert.Equal(VoxelSession.StakeResult.Marked, session.MarkPlot(middle.X, middle.Z).Result);
        _ = session.OpenTalk(Mayor);
        return session;
    }

    private static int Count(VoxelSession session, ItemId item) => session.Simulation.Inventory.Count(item);

    private static int Bought(VoxelSession session) => session.Events.OfType<WorldEvent>().Count(static e => e.Name == WorldEvents.PlotBought);

    private static string LastHostEvent(VoxelSession session) => Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent;

    private static (int Coins, int Kits, int Stakes, Plot? Owned, Plot? Pending, int Bought) Snapshot(VoxelSession session) =>
        (Count(session, ItemId.Coin), Count(session, ItemId.CampKit), Count(session, ItemId.Stake), session.OwnedPlot, session.PendingPlot, Bought(session));

    private static ObjectiveStep Step(VoxelSession session, string key) => session.ObjectiveStep(key)!;

    [Fact]
    public void Given_ASessionTwoWithNoStake_When_TheMayorsTalkOpens_Then_HeOffersTheStakeAndSiGivesIt()
    {
        var session = SessionTwo();

        var opening = session.OpenTalk(Mayor);

        Assert.True(opening.HostTurn);
        Assert.Equal(["Hola. ¿Quieres un terreno? Necesitas una estaca."], opening.Lines);
        Assert.Equal(StakePrompt, opening.Prompt);
        Assert.True(session.LandOffered);
        Assert.Equal(0, Count(session, ItemId.Stake));

        var turn = session.Say("sí");

        Assert.True(turn.Communicated);
        Assert.Equal("idle", turn.AgentState);
        Assert.Contains("villager.give_stake", turn.HostEvents);
        Assert.Contains("Pon la estaca", string.Join(' ', turn.Replies), StringComparison.Ordinal);
        Assert.Equal(1, Count(session, ItemId.Stake));
        Assert.False(session.LandOffered);
        session.Step([]);
        Assert.True(Step(session, "my-land.stake").Done); // the checklist's first step counts the bag
    }

    [Fact]
    public void Given_SessionOne_When_TheMayorsTalkOpens_Then_HeIsTheVillagerHeWasAndOffersNothing()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        Stage.TeleportToNpc(session, Mayor);

        var opening = session.OpenTalk(Mayor);

        Assert.Equal(["Hola."], opening.Lines);
        Assert.Equal("villager-open", opening.Prompt);
        Assert.False(session.LandOffered);
    }

    [Fact]
    public void Given_ADayTwoClassAtTheMayor_When_HeOffersTheStakeAndTheLearnerAnswers_Then_TheClassIsOfferedInTheSameTalkAfterwards()
    {
        var session = TestGame.NewSessionWithShippedLessons(LandTestScene.Seed);
        session.Lessons.CountAsCompleted("first-words", 1);
        session.Day = 2;
        session.Sitting = 2;
        Stage.TeleportToNpc(session, Mayor);

        var opening = session.OpenTalk(Mayor);

        Assert.Equal(StakePrompt, opening.Prompt); // the stake first, as an order's offer comes first (D-game-25)
        Assert.Null(session.Lessons.OfferStandingAt(Mayor)); // the class waits for the answer
        _ = session.Say("No, gracias.");
        Assert.Equal("personas", session.Lessons.OfferStandingAt(Mayor)); // and follows in the same talk
        Assert.Equal(0, Count(session, ItemId.Stake));
    }

    [Fact]
    public void Given_TheStakeOffer_When_TheLearnerSaysNoSeOrMissesTwice_Then_PatienceGivesTheStakeAnyway()
    {
        var session = SessionTwo();
        _ = session.OpenTalk(Mayor);
        var forward = session.Say("no sé");
        Assert.True(forward.FailForward);
        Assert.Equal(1, Count(session, ItemId.Stake));

        var again = SessionTwo();
        _ = again.OpenTalk(Mayor);
        Assert.False(again.Say("zzz qqq").FailForward);
        Assert.True(again.Say("azul").FailForward);
        Assert.Equal(1, Count(again, ItemId.Stake));
        Assert.Equal(new NpcPatienceRule(2, 1, "villager.stake_give", StakePrompt, false), TestGame.Data.Npcs.Find(Mayor)!.Patience);
    }

    [Fact]
    public void Given_TheStakeOfferAndAStakeHeldAlready_When_TheLearnerSaysSi_Then_NoSecondStakeIsGiven()
    {
        var session = SessionTwo();
        _ = session.OpenTalk(Mayor); // the offer stands: the player had no stake
        _ = session.Simulation.GrantItems(ItemId.Stake, 1);

        _ = session.Say("sí");

        Assert.Equal(1, Count(session, ItemId.Stake));
    }

    [Fact]
    public void Given_ThePlotIsMarked_When_TheTalkOpens_Then_HeSaysThePriceAndTheArrowWentToHim()
    {
        var session = SessionTwo();
        _ = session.MarkPlot(Corner.X + 5, Corner.Z + 5);
        Assert.Equal("Alcalde", session.Pointer!.Place); // marking a plot sends the player to the mayor
        var feet = session.Scene.Npc(Mayor).Feet;
        Assert.Equal(new GuideCell(feet.X, feet.Y, feet.Z), session.Pointer.Target);

        var opening = session.OpenTalk(Mayor);

        Assert.Equal(["El terreno cuesta diez monedas. ¿Lo quieres?"], opening.Lines);
        Assert.Equal(SalePrompt, opening.Prompt);
        Assert.True(session.LandOffered);
        Assert.Equal(10, session.Land.Rules.PlotPrice); // the line says «diez»: the price is game/land.json's
    }

    [Fact]
    public void Given_ThePurchaseMade_When_TheLearnerSaysSiToTheClassOffer_Then_TheVerdictIsConfirmUnderstoodAndTheClassStarts()
    {
        var session = AtTheSale(12);
        _ = session.Say("sí"); // the purchase
        Assert.Equal(1, Bought(session));
        Assert.NotNull(session.Lessons.OfferStandingAt(Mayor)); // the class follows in the same talk

        var turn = session.Say("sí");

        Assert.Equal("confirm", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.True(turn.LessonHandled);
        Assert.Equal("personas", session.Lessons.Active?.Id);
    }

    [Fact]
    public void Given_NoPlotPendingAtTheSale_When_TheLearnerSaysSi_Then_TheMayorAsksForTheStakeOnTheFieldAndNothingChanges()
    {
        var session = AtTheSale(12);
        session.ClearPendingPlot(); // the plot was taken off the map after the offer
        var before = Snapshot(session);

        var turn = session.Say("sí");

        Assert.Equal("villager.no_plot", LastHostEvent(session));
        Assert.Equal(["Primero pon la estaca en el campo."], turn.FollowUp);
        Assert.Empty(turn.Replies);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void Given_NoPlotAndNoStake_When_TheLearnerAsksForTheLand_Then_TheMayorGivesAStakeAndTakesNothing()
    {
        var session = SessionTwo(12);
        _ = session.OpenTalk(Mayor);
        _ = session.Say("No, gracias."); // the stake offer answered with no

        var turn = session.Say("Quiero un terreno, por favor.");

        Assert.Equal("villager.no_stake", LastHostEvent(session));
        Assert.Equal(["Primero necesitas una estaca. Toma."], turn.FollowUp);
        Assert.Equal(1, Count(session, ItemId.Stake));
        Assert.Equal(12, Count(session, ItemId.Coin));
        Assert.Null(session.OwnedPlot);
        Assert.Equal(0, Bought(session));
    }

    [Fact]
    public void Given_AStakeAndNoPlot_When_TheLearnerAsksForTheLandAtTheOpenPrompt_Then_TheMayorSaysToMarkItFirst()
    {
        var session = SessionTwo(12);
        _ = session.Simulation.GrantItems(ItemId.Stake, 1);
        _ = session.OpenTalk(Mayor);
        var before = Snapshot(session);

        var turn = session.Say("Quiero la tierra.");

        Assert.Equal("villager.no_plot", LastHostEvent(session));
        Assert.Equal(["Primero pon la estaca en el campo."], turn.FollowUp);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void Given_NineCoins_When_TheLearnerSaysSi_Then_TheMayorSaysHowToEarnCoinsAndNothingChanges()
    {
        var session = AtTheSale(9);
        var before = Snapshot(session);

        var turn = session.Say("sí");

        Assert.Equal("villager.cant_pay", LastHostEvent(session));
        Assert.Equal(["Necesitas diez monedas. Vende madera a Luz."], turn.FollowUp);
        Assert.Empty(turn.Replies); // «El terreno es tuyo» is not said
        Assert.Equal(before, Snapshot(session));
        Assert.Equal(9, Count(session, ItemId.Coin));
        Assert.NotNull(session.PendingPlot);
        Assert.Null(session.OwnedPlot);
        Assert.Equal(0, Count(session, ItemId.CampKit));
        Assert.Equal("idle", turn.AgentState); // the talk goes on at his open prompt; the next talk offers the plot again
        session.EndTalk();
        Assert.Equal(["El terreno cuesta diez monedas. ¿Lo quieres?"], session.OpenTalk(Mayor).Lines);
    }

    [Theory]
    [InlineData("sí")]
    [InlineData("sí, por favor")]
    [InlineData("Quiero un terreno, por favor.")]
    [InlineData("Quiero un terreno")]
    [InlineData("Quiero la tierra.")]
    public void Given_TenCoinsAndAMarkedPlot_When_TheLearnerSaysSiOrAsksForTheLand_Then_TheCoinsGoTheLandIsOwnedAndTheKitComesOnce(string line)
    {
        var session = AtTheSale(12);

        var turn = session.Say(line);

        Assert.True(turn.Communicated);
        Assert.Equal(2, Count(session, ItemId.Coin)); // ten out
        Assert.Equal(new Plot(Corner.X, Corner.Z, 10, 10), session.OwnedPlot);
        Assert.Null(session.PendingPlot);
        Assert.Equal(1, Count(session, ItemId.CampKit));
        Assert.Equal(1, Bought(session));
        Assert.Equal("idle", turn.AgentState);
        Assert.Contains("El terreno es tuyo. Toma el equipo.", string.Join(' ', turn.GuideLines), StringComparison.Ordinal);
        session.Step([]);
        Assert.True(Step(session, "my-land.buy").Done);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.PlotBought, Npc: Mayor });
        Assert.Contains(session.Locale.Ui("plotBought", ("price", "10")), session.TakeHints());
        Assert.Equal("terreno", session.Pointer!.Place); // after the purchase the arrow is on the plot
        Assert.Equal(new GuideCell(Corner.X + 5, session.Pointer.Target.Y, Corner.Z + 5), session.Pointer.Target);
        Assert.Equal("plot bought", AutosavePolicy.ReasonOf(session.Events.OfType<WorldEvent>().Last(static e => e.Name == WorldEvents.PlotBought)));
    }

    [Fact]
    public void Given_ThePurchaseIsMade_When_TheLandIsOwned_Then_TheChecklistIsDoneAndSleepIsNotHeldBack()
    {
        var session = AtTheSale(10);
        Assert.True(session.SessionComplete); // my-land ends no session: it is not part of the day's work
        Assert.False(Step(session, "my-land.buy").Done);

        _ = session.Say("sí");
        session.Step([]);

        Assert.Equal(0, Count(session, ItemId.Coin));
        Assert.True(Step(session, "my-land.buy").Done);
        Assert.False(session.Objectives.First(static o => o.Id == "my-land").Complete); // Y12: the camp and its fire are the checklist's other two steps
        Assert.True(session.SessionComplete);
        Assert.False(session.Objectives.First(static o => o.Id == "my-land").Spec.EndsSession);
        Assert.Equal(2, session.Objectives.First(static o => o.Id == "my-land").Spec.Session);
    }

    [Fact]
    public void Given_AStakeInTheBag_When_ThePlotIsBought_Then_TheMayorKeepsTheStakeAndTheStakeStepStaysDone()
    {
        var session = AtTheSale(10);
        session.Step([]);
        Assert.Equal(1, Count(session, ItemId.Stake));
        Assert.True(Step(session, "my-land.stake").Done);

        _ = session.Say("sí");
        session.Step([]);

        Assert.Equal(0, Count(session, ItemId.Stake));
        Assert.Equal(1, Count(session, ItemId.CampKit));
        Assert.True(Step(session, "my-land.stake").Done);
        Assert.True(Step(session, "my-land.buy").Done);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemTaken, Npc: Mayor } && e.Detail?.StartsWith("estaca", StringComparison.Ordinal) == true);
        Assert.Contains("The mayor keeps the stake", string.Join(' ', session.TakeHints()), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NoStakeInTheBag_When_AMarkedPlotIsBought_Then_TheSaleGoesThrough()
    {
        var session = SessionTwo(10);
        Assert.Equal(VoxelSession.StakeResult.Marked, session.MarkPlot(Corner.X + 5, Corner.Z + 5).Result); // the launch flag's way: no stake held
        Assert.Equal(0, Count(session, ItemId.Stake));
        _ = session.OpenTalk(Mayor);

        _ = session.Say("sí");

        Assert.NotNull(session.OwnedPlot);
        Assert.Equal((0, 1, 0), (Count(session, ItemId.Coin), Count(session, ItemId.CampKit), Count(session, ItemId.Stake)));
    }

    [Fact]
    public void Given_AStakeHeldAndTheSaleRefused_When_TheLearnerSaysSi_Then_TheStakeStaysInTheBag()
    {
        // cant_pay
        var poor = AtTheSale(9);
        _ = poor.Say("sí");
        Assert.Equal(1, Count(poor, ItemId.Stake));

        // no_plot (the marked ground no longer passes the land rules)
        var spoiled = AtTheSale(12);
        var (x, z) = (Corner.X + 3, Corner.Z + 4);
        Assert.True(spoiled.Scene.Grid.SetBlock(x, LandRules.GroundOf(spoiled.Scene.Grid, x, z).Y, z, BlockId.Stone));
        _ = spoiled.Say("sí");
        Assert.Equal(1, Count(spoiled, ItemId.Stake));

        // a sale the session refuses at the last moment (the world moved after the turn)
        var moved = AtTheSale(12);
        var cell = (X: Corner.X + 2, Z: Corner.Z + 2);
        _ = moved.Scene.Grid.SetBlock(cell.X, LandRules.GroundOf(moved.Scene.Grid, cell.X, cell.Z).Y, cell.Z, BlockId.Stone);
        Assert.False(moved.SellPlot(Mayor, TestGame.Data.HostEvents.Of(Mayor, "villager.buy_plot"), "test"));
        Assert.Equal(1, Count(moved, ItemId.Stake));
    }

    [Fact]
    public void Given_AFullBagWhoseOnlyFreeableSlotsAreTheCoinsAndTheStake_When_ThePlotIsBought_Then_TheStakesSlotMakesRoomForTheKit()
    {
        var session = AtTheSale(20); // ten coins stay: the coins' slot is not freed by the price
        _ = session.Simulation.GrantItems(TestItems.Axe, 28); // book, coins, stake and 27 axes fill the 30 slots
        Assert.False(session.Simulation.Inventory.CanAdd(ItemId.CampKit, 1));
        Assert.Equal(1, Count(session, ItemId.Stake));

        _ = session.Say("sí");

        Assert.NotNull(session.OwnedPlot);
        Assert.Equal((10, 1, 0), (Count(session, ItemId.Coin), Count(session, ItemId.CampKit), Count(session, ItemId.Stake)));
    }

    [Fact]
    public void Given_ThePurchaseIsSaved_When_TheGameLoadsWithNoStakeInTheBag_Then_TheStakeStepIsStillDone()
    {
        var session = AtTheSale(10);
        _ = session.Say("sí");
        session.Step([]);

        var loaded = SaveGame.Load(SaveGame.Save(session, Pristine()), TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);
        loaded.Step([]);

        Assert.Equal(0, Count(loaded, ItemId.Stake));
        Assert.True(loaded.ObjectiveStep("my-land.stake")!.Done);
        Assert.True(loaded.ObjectiveStep("my-land.buy")!.Done);
    }

    [Fact]
    public void Given_AFullBag_When_TheLearnerSaysSi_Then_TheMayorSaysThereIsNoRoomAndNothingChanges()
    {
        var session = SessionTwo(20);
        _ = session.MarkPlot(Corner.X + 5, Corner.Z + 5); // no stake held: its slot would make room for the kit
        _ = session.OpenTalk(Mayor);
        _ = session.Simulation.GrantItems(TestItems.Axe, 28); // the book, the coins and 28 axes fill the 30 slots (with no stake to hand over); ten coins leave the coin slot occupied
        Assert.False(session.Simulation.Inventory.CanAdd(ItemId.CampKit, 1));
        var before = Snapshot(session);
        var axes = Count(session, TestItems.Axe);

        var turn = session.Say("sí");

        Assert.Equal("villager.no_room", LastHostEvent(session));
        Assert.Equal(["Tu bolsa está llena. No hay lugar para el equipo."], turn.FollowUp);
        Assert.Equal(before, Snapshot(session));
        Assert.Equal(axes, Count(session, TestItems.Axe));
        Assert.NotNull(session.PendingPlot);
    }

    [Fact]
    public void Given_TheExactCoinsInTheOnlyCoinSlotOfAFullBag_When_TheLearnerSaysSi_Then_PayingFreesTheSlotAndTheKitFits()
    {
        var session = AtTheSale(10);
        _ = session.Simulation.GrantItems(TestItems.Axe, 28); // full, but the ten coins are the whole coin stack: once paid, their slot is free
        Assert.False(session.Simulation.Inventory.CanAdd(ItemId.CampKit, 1));

        _ = session.Say("sí");

        Assert.Equal((0, 1), (Count(session, ItemId.Coin), Count(session, ItemId.CampKit)));
        Assert.NotNull(session.OwnedPlot);
    }

    [Fact]
    public void Given_ThePlotWasBought_When_TheLearnerAsksForAStakeAgain_Then_NoStakeIsGivenAndTheStakesUseMarksNothing()
    {
        var session = AtTheSale(10);
        _ = session.Say("sí");
        session.EndTalk();
        _ = session.OpenTalk(Mayor);

        _ = session.Say("Quiero una estaca");

        Assert.Equal(0, Count(session, ItemId.Stake));
        Assert.False(session.LandOffered);
        Assert.Equal(VoxelSession.StakeResult.NotApplied, session.UseStake(new BlockPos(Corner.X + 5, LandRules.GroundOf(session.Scene.Grid, Corner.X + 5, Corner.Z + 5).Y, Corner.Z + 5)));
        Assert.Null(session.PendingPlot);
    }

    [Fact]
    public void Given_ThePlotWasBought_When_TheLearnerAsksForLandAgain_Then_TheMayorSaysItIsTheirsAndNothingMoves()
    {
        var session = AtTheSale(30);
        _ = session.Say("sí");
        var after = Snapshot(session);
        Assert.Equal(20, after.Coins);

        var again = session.Say("No, gracias."); // the class offer that follows the purchase, declined (understood, Y14): nothing moves
        Assert.True(again.Communicated);
        Assert.Equal(after, Snapshot(session));

        var asked = session.Say("Quiero un terreno, por favor.");
        Assert.Equal("villager.already_owned", LastHostEvent(session));
        Assert.Equal(["El terreno ya es tuyo."], asked.FollowUp);
        Assert.Equal(after, Snapshot(session));

        session.EndTalk();
        var reopened = session.OpenTalk(Mayor); // a second talk offers nothing more
        Assert.Equal(["Hola."], reopened.Lines);
        Assert.False(session.LandOffered);
        _ = session.Say("Quiero la tierra.");
        Assert.Equal(after, Snapshot(session));
    }

    [Fact]
    public void Given_TheOfferStandsAndTheLandBecomesOwned_When_TheLearnerSaysSi_Then_NoSecondPaymentOrKitIsTaken()
    {
        var session = AtTheSale(30);
        Assert.True(session.OwnPlot()); // the land was taken another way after the offer was made
        _ = session.Simulation.GrantItems(ItemId.CampKit, 1);
        var before = Snapshot(session);

        _ = session.Say("sí");

        Assert.Equal("villager.already_owned", LastHostEvent(session));
        Assert.Equal(before, Snapshot(session));
        Assert.Equal(0, Bought(session));
    }

    [Fact]
    public void Given_ThePurchaseIsSaved_When_TheGameLoadsAndTheLearnerTalksAgain_Then_TheLandIsTheirsAndNothingIsPaidOrGivenTwice()
    {
        var session = AtTheSale(30);
        _ = session.Say("sí");
        session.Step([]);
        var after = Snapshot(session);
        var text = SaveGame.Save(session, Pristine());

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(new Plot(Corner.X, Corner.Z, 10, 10), loaded.OwnedPlot);
        Assert.True(loaded.ObjectiveStep("my-land.buy")!.Done); // restored from systems.land.owned
        Assert.False(loaded.Objectives.First(static o => o.Id == "my-land").Complete); // Y12: the camp and its fire are still to do
        Assert.Equal((after.Coins, after.Kits, after.Stakes, after.Owned, after.Pending), (Count(loaded, ItemId.Coin), Count(loaded, ItemId.CampKit), Count(loaded, ItemId.Stake), loaded.OwnedPlot, loaded.PendingPlot));
        Assert.Equal(0, Bought(loaded)); // restored, not raised again
        Stage.TeleportToNpc(loaded, Mayor);
        var opening = loaded.OpenTalk(Mayor);
        Assert.Equal(["Hola."], opening.Lines);
        _ = loaded.Say("Quiero un terreno, por favor.");
        Assert.Equal("villager.already_owned", LastHostEvent(loaded));
        Assert.Equal((after.Coins, after.Kits), (Count(loaded, ItemId.Coin), Count(loaded, ItemId.CampKit)));
        Assert.Equal(0, Bought(loaded));
    }

    [Fact]
    public void Given_AMarkedPlotAndAMarkedSaveBeforeTheSale_When_TheGameLoads_Then_TheStakeStepAndThePendingPlotComeBack()
    {
        var session = SessionTwo(0);
        _ = session.OpenTalk(Mayor);
        _ = session.Say("sí"); // the stake
        _ = session.MarkPlot(Corner.X + 5, Corner.Z + 5);
        session.EndTalk();
        session.Step([]);

        var loaded = SaveGame.Load(SaveGame.Save(session, Pristine()), TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);
        loaded.Step([]);

        Assert.Equal(new Plot(Corner.X, Corner.Z, 10, 10), loaded.PendingPlot);
        Assert.True(loaded.ObjectiveStep("my-land.stake")!.Done);
        Assert.False(loaded.ObjectiveStep("my-land.buy")!.Done);
        Stage.TeleportToNpc(loaded, Mayor);
        Assert.Equal(["El terreno cuesta diez monedas. ¿Lo quieres?"], loaded.OpenTalk(Mayor).Lines); // a load puts the offer back
    }

    [Fact]
    public void Given_AMarkedPlotAndTenCoins_When_TheGroundBecomesStoneBeforeTheSale_Then_TheSaleIsRefusedAndNothingChanges()
    {
        var session = AtTheSale(12);
        var (x, z) = (Corner.X + 3, Corner.Z + 4);
        var ground = LandRules.GroundOf(session.Scene.Grid, x, z).Y;
        Assert.True(session.Scene.Grid.SetBlock(x, ground, z, BlockId.Stone)); // a block that is no grass or earth: the plot no longer passes the land rules
        Assert.False(session.Land.Rules.CheckPlot(session.Scene.Grid, session.PendingPlot!.Value).Ok);
        var before = Snapshot(session);

        var turn = session.Say("sí");

        Assert.Equal("villager.no_plot", LastHostEvent(session));
        Assert.Equal(["Primero pon la estaca en el campo."], turn.FollowUp);
        Assert.Equal(before, Snapshot(session));
        Assert.Null(session.OwnedPlot);
    }

    [Fact]
    public void Given_TheWorldMovesBetweenTheTurnAndItsApplication_When_TheSessionSells_Then_OwnPlotRefusesAndTheCoinsStay()
    {
        var session = AtTheSale(12);
        var effect = TestGame.Data.HostEvents.Of(Mayor, "villager.buy_plot");
        var (x, z) = (Corner.X + 2, Corner.Z + 2);
        var ground = LandRules.GroundOf(session.Scene.Grid, x, z).Y;
        _ = session.Scene.Grid.SetBlock(x, ground, z, BlockId.Stone);
        var before = Snapshot(session);

        var sold = session.SellPlot(Mayor, effect, "test");

        Assert.False(sold);
        Assert.Equal(before, Snapshot(session));
        Assert.Equal(12, Count(session, ItemId.Coin));
        Assert.Equal(0, Count(session, ItemId.CampKit));
    }

    [Fact]
    public void Given_TheSession_When_ThePlotIsSoldThroughTheSessionWithTheBagAsItIs_Then_TheOrderIsChecksThenOwnershipThenTheExchange()
    {
        // the session's exchange on its own: coins 10 out, the plot owned, the kit in, plot_bought once, the kit logged as a gift
        var session = AtTheSale(10);
        var effect = TestGame.Data.HostEvents.Of(Mayor, "villager.buy_plot");

        Assert.True(session.SellPlot(Mayor, effect, "test"));
        Assert.False(session.SellPlot(Mayor, effect, "test")); // a second call finds the land owned: no second payment or kit

        Assert.Equal((0, 1, 1), (Count(session, ItemId.Coin), Count(session, ItemId.CampKit), Bought(session)));
        var events = session.Events.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.True(events.IndexOf(WorldEvents.ItemTaken) < events.IndexOf(WorldEvents.ItemGiven));
        Assert.True(events.IndexOf(WorldEvents.ItemGiven) < events.IndexOf(WorldEvents.PlotBought));
    }

    [Theory]
    [InlineData(true, false, 10, 0, NpcHostEvents.AlreadyOwned)]
    [InlineData(false, false, 10, 1, NpcHostEvents.NoPlot)]
    [InlineData(false, false, 10, 0, NpcHostEvents.NoStake)]
    [InlineData(false, true, 9, 0, NpcHostEvents.CantPay)]
    [InlineData(false, true, 10, 0, null)]
    [InlineData(false, true, 11, 0, null)]
    public void Given_TheLandAndTheBag_When_ThePlotIsPriced_Then_TheMissIsTheFirstOfAlreadyOwnedNoPlotNoStakeCantPayNoRoom(bool owned, bool marked, int coins, int stakes, string? miss)
    {
        var bag = new Inventory(TestItems.Catalogue);
        if (coins > 0)
        {
            Assert.Equal(0, bag.Add(ItemId.Coin, coins));
        }

        if (stakes > 0)
        {
            Assert.Equal(0, bag.Add(ItemId.Stake, stakes));
        }

        var effect = new NpcEffect(NpcEffectKind.SellPlot, ItemId.CampKit);

        Assert.Equal(miss, effect.PlotMiss(bag, new PlotFacts(owned, marked, 10)));
    }

    [Fact]
    public void Given_ABagWithNoRoomOnceTheCoinsAreOut_When_ThePlotIsPriced_Then_TheMissIsNoRoom()
    {
        var bag = new Inventory(TestItems.Catalogue);
        Assert.Equal(0, bag.Add(ItemId.Coin, 20));
        Assert.Equal(0, bag.Add(TestItems.Axe, bag.SlotCount - 1)); // the coins' slot and the axes fill the bag; ten coins stay after the price
        var effect = new NpcEffect(NpcEffectKind.SellPlot, ItemId.CampKit);

        Assert.Equal(NpcHostEvents.NoRoom, effect.PlotMiss(bag, new PlotFacts(false, true, 10)));
        Assert.Equal(NpcHostEvents.NoStake, effect.PlotMiss(bag, null)); // a world with no land: nothing is marked
    }

    [Fact]
    public void Given_TheShippedBundle_When_ItIsValidated_Then_TheMayorsSaleOffersAndMissesAreAllThereAndTheKindIsKnown()
    {
        Assert.Contains("sell_plot", HostEventTable.Kinds);
        Assert.Equal(NpcEffectKind.SellPlot, TestGame.Data.HostEvents.Of(Mayor, "villager.buy_plot").Kind);
        Assert.Equal(ItemId.CampKit, TestGame.Data.HostEvents.Of(Mayor, "villager.buy_plot").Item);
        Assert.Equal(Mayor, TestGame.Data.HostEvents.NpcWith(NpcEffectKind.SellPlot));
        Assert.Equal(ItemId.Stake, TestGame.Data.HostEvents.Of(Mayor, "villager.give_stake").Item);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    [Fact]
    public void Given_ASellPlotRowForAnNpcWhoCannotSayItsMisses_When_TheBundleIsValidated_Then_EveryMissingStateAndOfferIsAProblem()
    {
        const string Table = """{ "schemaVersion": 2, "events": [ { "npc": "fisher", "event": "fisher.sell_plot", "action": { "kind": "sell_plot", "item": "camp-kit" } } ] }""";

        var problems = GameDataValidator.Validate(TestGame.Data.WithHostEvents(HostEventTable.Parse(Table, TestItems.Catalogue))).Where(static p => p.Contains("fisher", StringComparison.Ordinal)).ToList();

        foreach (var miss in new[] { NpcHostEvents.AlreadyOwned, NpcHostEvents.NoPlot, NpcHostEvents.NoStake, NpcHostEvents.CantPay, NpcHostEvents.NoRoom })
        {
            Assert.Contains(problems, p => p.Contains($"takes 'fisher.{miss}'", StringComparison.Ordinal));
        }

        Assert.Contains(problems, static p => p.Contains("does not take 'fisher.offer_stake'", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.Contains("does not take 'fisher.offer_plot'", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ASellPlotRowWithNoItem_When_TheTableIsParsed_Then_ItIsRefused()
    {
        const string Table = """{ "schemaVersion": 2, "events": [ { "npc": "villager", "event": "villager.buy_plot", "action": { "kind": "sell_plot" } } ] }""";

        _ = Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(Table, TestItems.Catalogue));
    }

    [Fact]
    public void Given_TheCampKit_When_ItIsLookedUp_Then_ItIsAnItemOfStackOneWithItsNounAndItsLabel()
    {
        Assert.Equal(("camp-kit", 1), (TestItems.Catalogue.Get(ItemId.CampKit).Name, TestItems.Catalogue.Get(ItemId.CampKit).MaxStack));
        var noun = TestGame.Nouns.ForItem(ItemId.CampKit)!;
        Assert.Equal("equipo", noun.Lemma);
        Assert.Equal("el equipo", noun.WithDefinite);
        Assert.Equal("equipo de campamento", TestGame.Nouns.LabelOf(ItemId.CampKit));
        Assert.Equal("terreno", TestGame.Nouns.ForLemma("terreno")!.Lemma);
    }

    [Fact]
    public void Given_TheMayor_When_TheRosterIsRead_Then_HeIsTheAlcaldeAndHisPlotPromptsAreMappedToTheOfferStates()
    {
        var entry = TestGame.Data.Npcs.Find(Mayor)!;

        Assert.Equal("Alcalde", entry.Label);
        Assert.Equal("mayor-stake", entry.Prompts[NpcHostEvents.StakeOfferState]);
        Assert.Equal("mayor-sell", entry.Prompts[NpcHostEvents.PlotOfferState]);
        Assert.Equal("The mayor", TestGame.Locale.NpcName(Mayor));
        Assert.Contains("quiero-un-terreno", entry.Focus["mayor-sell"]);
    }
}
