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
    private readonly Queue<int> _pending = new();

    /// <summary>Creates the panel's behaviour.</summary>
    /// <param name="crafting">The panel's state.</param>
    /// <param name="session">The session whose recipes and words the panel shows.</param>
    /// <param name="sound">Plays the refusal.</param>
    /// <param name="rowAt">The panel's recipe row at a point of the canvas, or <see langword="null"/> (the panel's layout, from the game).</param>
    /// <param name="otherPanelOpen">Whether the book, the inventory or the dialogue is open (the crafting panel does not open over them).</param>
    public CraftingPanel(CraftingScreen crafting, VoxelSession session, SoundFrontend sound, Func<int, int, int?> rowAt, Func<bool> otherPanelOpen)
    {
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

    /// <summary>The panel's keys while it is open: Esc or C closes it.</summary>
    public void HandleInput(IInputFrame input)
    {
        if (input.Pressed(GameKey.Escape) || input.Pressed(GameKey.C))
        {
            Close();
        }
    }

    /// <summary>A click on a row of the open panel asks for one craft of its recipe in the next tick.</summary>
    public void Click(IInputFrame input)
    {
        if (_rowAt(input.PointerX, input.PointerY) is not { } row)
        {
            return;
        }

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
