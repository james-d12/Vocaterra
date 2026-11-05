namespace Vocaterra.Game.Core.Dialogue;

/// <summary>One row of the dialogue panel (U02): a line of the page, or the coaching line.</summary>
/// <param name="Index">The line's index in <see cref="DialogueModel.Lines"/>, or -1 for the coaching line.</param>
/// <param name="Line">The line.</param>
public sealed record PanelRow(int Index, DialogueLine Line);
