namespace Vocaterra.Game.UI;

/// <summary>What the dialogue panel is doing with the keyboard: showing the speech buttons (the keys R, S, H, N, W, 1 to 3 press them, V or Space talks) or the text box.</summary>
public enum PanelMode
{
    /// <summary>The speech buttons; the keys are shortcuts, nothing is typed.</summary>
    Buttons,

    /// <summary>The text box has the keyboard (the Escribir button, Tab or W opened it).</summary>
    Typing,
}
