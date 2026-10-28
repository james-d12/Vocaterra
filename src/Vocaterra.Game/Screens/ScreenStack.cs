using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Screens;

/// <summary>
/// The screens over the world in the order they take the keyboard: the first open one is on top and gets the frame's keys, and the world gets
/// them only when none is open. The report panel (S21) is not one of them: it takes every key from anywhere while it is open.
/// </summary>
public sealed class ScreenStack
{
    private readonly IReadOnlyList<IScreen> _inputOrder;
    private readonly IReadOnlyList<IScreen> _nameOrder;

    /// <summary>Creates the stack.</summary>
    /// <param name="inputOrder">The screens, the one that takes the keyboard first at the front.</param>
    /// <param name="nameOrder">
    /// The same screens in the order a report names the open one. It differs from the input order where two can be open at once: a playtest's
    /// mic check comes before its pre-check and takes the keys, while the report names the check (kept as it was; a finding, R16).
    /// </param>
    public ScreenStack(IReadOnlyList<IScreen> inputOrder, IReadOnlyList<IScreen> nameOrder)
    {
        _inputOrder = inputOrder;
        _nameOrder = nameOrder;
    }

    /// <summary>The open screen that takes the keyboard, or <see langword="null"/> when the world has it.</summary>
    public IScreen? Top => _inputOrder.FirstOrDefault(static s => s.IsOpen);

    /// <summary>Whether an open screen frees the mouse and holds the world's input.</summary>
    public bool FreesMouse => _inputOrder.Any(static s => s.IsOpen && s.FreesMouse);

    /// <summary>Gives the frame's input to the top screen. Returns whether a screen took it.</summary>
    public bool HandleInput(IInputFrame input)
    {
        if (Top is not { } top)
        {
            return false;
        }

        top.HandleInput(input);
        return true;
    }

    /// <summary>What is open now, in the words of a report's table.</summary>
    public string OpenScreenName() => _nameOrder.FirstOrDefault(static s => s.IsOpen)?.Name ?? "none (the world)";
}
