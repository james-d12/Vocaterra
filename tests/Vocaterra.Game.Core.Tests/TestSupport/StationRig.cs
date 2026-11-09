using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>The rows' sound for the station tests: whether a row can be heard, and what was played.</summary>
internal sealed class FakeInstructionAudio : IInstructionAudio
{
    /// <summary>Whether a row can be heard.</summary>
    public bool Playable { get; set; } = true;

    /// <summary>The rows played, with <c>@slow</c> after the slow ones.</summary>
    public List<string> Played { get; } = [];

    public bool CanPlay(string rowId) => Playable;

    public bool Play(string rowId, bool slow)
    {
        if (!Playable)
        {
            return false;
        }

        Played.Add(slow ? rowId + "@slow" : rowId);
        return true;
    }
}

/// <summary>
/// The station over the three-step test procedure of <see cref="ProcedureRig"/> (wood and water on the table in either order, then the bread out of the oven with the hand): a
/// session with a wood and a water-bottle in the bag, the actions of the stations (the oven's mouth has two verbs, <c>sacar</c> and <c>abrir</c>), the overlay and its panel.
/// </summary>
internal sealed class StationRig
{
    public const string Actions = """
        { "schemaVersion": 1, "actions": [
          { "use": "madera", "on": "mesa", "verbs": ["poner"] },
          { "use": "agua", "on": "mesa", "verbs": ["echar"] },
          { "use": "mano", "on": "horno", "verbs": ["sacar", "abrir"] } ] }
        """;

    /// <param name="locale">The locale the session reads its words from, or <see langword="null"/> for the test locale.</param>
    public StationRig(bool playable = true, Func<bool>? otherPanelOpen = null, LocaleText? locale = null)
    {
        Audio = new FakeInstructionAudio { Playable = playable };
        var data = ProcedureRig.Data().WithActions(ActionTable.Parse(Actions));
        if (locale is not null)
        {
            data = data.WithLocale(locale);
        }

        Session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped, TestGame.NoLessons);
        _ = Session.Simulation.Inventory.Add(ItemId.Wood, 1);
        _ = Session.Simulation.Inventory.Add(ItemId.WaterBottle, 1);
        Session.MeetOrderNpcs(); // the NPCs with orders met on an earlier visit (issue #55): a talk with Luz opens with her offer to bake, not her order
        Panel = new StationPanel(Screen, Session, Audio, (_, _) => null, otherPanelOpen ?? (() => false));
    }

    public FakeInstructionAudio Audio { get; }

    public VoxelSession Session { get; }

    public StationScreen Screen { get; } = new();

    public StationPanel Panel { get; }

    public StationView View => Panel.View();

    /// <summary>Presses keys in one frame.</summary>
    public void Press(params GameKey[] keys) => Panel.HandleInput(TestInputFrame.Pressing(keys));

    /// <summary>Presses the digit of a held thing on the current page (by its noun).</summary>
    public void Hold(string noun)
    {
        var key = View.Held.First(c => c.Noun == noun).Key!.Value;
        Press((GameKey)((int)GameKey.D0 + key));
    }

    /// <summary>Selects the target by Tab until it is the one.</summary>
    public void Aim(string noun)
    {
        for (var i = 0; i < 8 && View.Targets.FirstOrDefault(static t => t.Selected)?.Noun != noun; i++)
        {
            Press(GameKey.Tab);
        }
    }

    /// <summary>A whole bake with every step right, from the open station; the bag is filled again for the next one.</summary>
    public void Bake()
    {
        foreach (var id in Session.Procedures.Order.ToList())
        {
            DoStep(id);
        }

        _ = Session.Simulation.Inventory.Add(ItemId.Wood, 1);
        _ = Session.Simulation.Inventory.Add(ItemId.WaterBottle, 1);
    }

    /// <summary>Starts the next bake (Enter once the last is done), after the time between two sittings or acts has passed.</summary>
    /// <param name="seconds">The stored time to let pass.</param>
    public void NextBake(long seconds = 0)
    {
        Session.SkipTime(seconds);
        Press(GameKey.Enter);
    }

    /// <summary>The keys of the step's act: hold its noun, aim at its target, Enter.</summary>
    public void DoStep(string stepId)
    {
        var (use, on, verb) = ProcedureRig.ActOf(stepId);
        if (use == "mano")
        {
            Press(GameKey.D0);
        }
        else
        {
            Hold(use);
        }

        Aim(on);
        Press(GameKey.Enter);
        if (View.Verbs is { } choice)
        {
            Press((GameKey)((int)GameKey.D1 + choice.Verbs.ToList().IndexOf(verb))); // a pair of several verbs asks which
        }
    }
}
