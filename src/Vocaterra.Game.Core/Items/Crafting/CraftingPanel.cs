using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>
/// The crafting panel (C anywhere, or E at the workbench): a click on a row asks for one craft in the next tick, the same command a script sends;
/// a craft whose ingredients are not there yet sounds a soft refusal (S24). Esc or C closes it.
/// </summary>
public sealed class CraftingPanel : IScreen
{
    private readonly CraftingScreen _crafting;
    private readonly VoxelSession _session;
    private readonly SoundFrontend _sound;
    private readonly Func<int, int, int?> _rowAt;
    private readonly Func<bool> _otherPanelOpen;
    private readonly Func<(int Max, int Page)> _scrollRange;
    private readonly Queue<int> _pending = new();

    /// <summary>Creates the panel's behaviour.</summary>
    /// <param name="crafting">The panel's state.</param>
    /// <param name="session">The session whose recipes and words the panel shows.</param>
    /// <param name="sound">Plays the refusal.</param>
    /// <param name="rowAt">The panel's recipe row at a point of the canvas, or <see langword="null"/> (the panel's layout, from the game).</param>
    /// <param name="otherPanelOpen">Whether the book, the inventory or the dialogue is open (the crafting panel does not open over them).</param>
    /// <param name="scrollRange">The panel's largest scroll and its page, in rows, for the canvas as it is now (the layout's); <see langword="null"/> when the panel never scrolls.</param>
    public CraftingPanel(CraftingScreen crafting, VoxelSession session, SoundFrontend sound, Func<int, int, int?> rowAt, Func<bool> otherPanelOpen, Func<(int Max, int Page)>? scrollRange = null)
    {
        _scrollRange = scrollRange ?? (static () => (0, 1));
        _crafting = crafting;
        _session = session;
        _sound = sound;
        _rowAt = rowAt;
        _otherPanelOpen = otherPanelOpen;
    }

    /// <inheritdoc />
    public string Name => "crafting";

    /// <inheritdoc />
    public bool IsOpen => _crafting.IsOpen;

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>Opens the panel with the recipes' nouns, unless another panel is open.</summary>
    public void Open()
    {
        if (_otherPanelOpen())
        {
            return;
        }

        _crafting.Open(_session.Words, CraftingScreen.LemmasShown(_session.Recipes));
    }

    /// <summary>Closes the panel.</summary>
    public void Close() => _crafting.Close();

    /// <summary>The panel's keys while it is open: Esc or C closes it; the wheel and Up, Down, Page Up and Page Down scroll the rows when they do not all fit (I10b).</summary>
    public void HandleInput(IInputFrame input)
    {
        if (input.Pressed(GameKey.Escape) || input.Pressed(InputKeys.Of("crafting")))
        {
            Close();
            return;
        }

        var (max, page) = _scrollRange();
        var rows = 0;
        if (input.Wheel != 0)
        {
            rows -= (input.Wheel > 0 ? 1 : -1) * Math.Max(1, (int)Math.Round(Math.Abs(input.Wheel) / 120.0)) * 2; // a notch up goes up two rows
        }

        rows += input.Pressed(GameKey.Down) ? 1 : 0;
        rows -= input.Pressed(GameKey.Up) ? 1 : 0;
        rows += input.Pressed(GameKey.PageDown) ? Math.Max(page, 1) : 0;
        rows -= input.Pressed(GameKey.PageUp) ? Math.Max(page, 1) : 0;
        if (rows != 0)
        {
            _crafting.ScrollBy(rows, max);
        }
    }

    /// <summary>A click on a row of the open panel asks for one craft of its recipe in the next tick.</summary>
    public void Click(IInputFrame input)
    {
        if (_rowAt(input.PointerX, input.PointerY) is not { } row)
        {
            return;
        }

        var rows = CraftingScreen.Rows(_session.Simulation, _session.Recipes);
        if ((uint)row >= (uint)rows.Count)
        {
            return;
        }

        row = rows[row].Index; // the click is on a place of the panel (repair rows come and go, I07); the craft is the recipe's index
        if (_session.Simulation.CanCraft(row) != CraftResult.Done)
        {
            _sound.Queue(SoundCue.CraftRefused); // soft: the ingredients are not there yet (S24)
        }

        _pending.Enqueue(row);
    }

    /// <summary>The crafts asked for since the last tick, as the tick's commands (after the gathering and the placing: the simulation's order).</summary>
    public void TakeCrafts(List<VoxelCommand> commands)
    {
        while (_pending.Count > 0)
        {
            commands.Add(VoxelCommand.Craft(_pending.Dequeue()));
        }
    }
}
