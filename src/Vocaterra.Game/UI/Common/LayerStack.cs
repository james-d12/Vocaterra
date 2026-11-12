using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.UI.Book;
using Vocaterra.Game.UI.Checks;
using Vocaterra.Game.UI.Crafting;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;
using Vocaterra.Game.UI.Inventory;
using Vocaterra.Game.UI.Lesson;
using Vocaterra.Game.UI.Menu;
using Vocaterra.Game.UI.Overlays;
using Vocaterra.Game.UI.Report;
using Vocaterra.Game.UI.Station;

namespace Vocaterra.Game.UI.Common;

/// <summary>The 2D layers over the voxel world, bottom to top: the crosshair and the beacon's arrow, the HUD, the panels, the checks, the report panel at the top (S21), and the loading screen over everything.</summary>
public static class LayerStack
{
    /// <summary>The layers of a run.</summary>
    /// <param name="debug">The debug overlay (F3).</param>
    /// <param name="lesson">The lesson strip's state.</param>
    /// <param name="playtest">Whether the run is a playtest (it has the playtest's hint).</param>
    public static IReadOnlyList<IFrameLayer> Build(VoxelDebugOverlay debug, LessonFrontend lesson, bool playtest)
    {
        var layers = new List<IFrameLayer>
        {
            new FadeLayer(),
            new DeathScreenLayer(),
            new ViewEffectsLayer(),
            new CrosshairLayer(),
            new BeaconArrowLayer(),
            new TalkHintLayer(),
            new SleepPromptLayer(),
            new PlotHudLayer(),
            debug,
            new HotbarLayer(),
            new NeedsHudLayer(),
            new BreathHudLayer(),
            new RecipePanelLayer(),
            new ObjectiveLayer(),
            new TipLayer(),
            new SupportLayer(),
            new LessonLayer(lesson),
            new WordCardLayer(),
            new DialogueBubbleLayer(),
            new DialoguePanel(),
            new ToastLayer(),
            new EndingLayer(),
            new BookLayer(),
            new InventoryLayer(),
            new CraftingLayer(),
            new StationLayer(),
            new MenuLayer(),
            new CheckLayer(),
            new MicCheckLayer(),
            new ReportLayer(),
        };
        if (playtest)
        {
            layers.Add(new PlaytestHintLayer());
        }

        layers.Add(new LoadingLayer());
        return layers;
    }
}
