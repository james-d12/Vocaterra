using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Frame;
using Vocaterra.Game.Screens;
using Vocaterra.Game.UI;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests.Frame;

/// <summary>One tick of the world: the look first, then the session's step, then the observers in their order; and the crash hook of X05.</summary>
public sealed class WorldStepperTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));
    private static readonly UiCanvas Canvas = new(100, static () => (1280, 720));

    [Fact]
    public void TheLookGoesFirstAndTheObserversFollowTheStepInTheirOrder()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var seen = new List<string>();
        var stepper = Make(session, null, new DelegateTickObserver(c => seen.Add($"first {c[0].Kind} at {session.Simulation.Tick}")), new DelegateTickObserver(_ => seen.Add("second")));
        stepper.Step();
        stepper.Step();
        Assert.Equal(["first Look at 1", "second", "first Look at 2", "second"], seen);
    }

    [Fact]
    public void TheDebugCrashComesOutOfTheStepAtItsTick()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var stepper = Make(session, crashAfter: 2);
        stepper.Step();
        var error = Assert.Throws<InvalidOperationException>(stepper.Step);
        Assert.Contains("--debug-crash-after 2", error.Message, StringComparison.Ordinal);
    }

    private static WorldStepper Make(VoxelSession session, long? crashAfter, params ITickObserver[] observers)
    {
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());
        var crafting = new CraftingPanel(new CraftingScreen(), session, sound, (x, y) => new CraftingLayout(Canvas.Width, Canvas.Height, session.Recipes.Entries.Count).RowAt(x, y), static () => false);
        return new WorldStepper(session, new LookControl(), null, new LiveVoxelInput(), static () => false, static () => false, crafting, crashAfter, observers);
    }
}
