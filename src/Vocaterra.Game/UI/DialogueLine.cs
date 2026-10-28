namespace Vocaterra.Game.UI;

/// <summary>One line of the dialogue panel.</summary>
/// <param name="Speaker">Whose line it is.</param>
/// <param name="Text">The text.</param>
public sealed record DialogueLine(DialogueSpeaker Speaker, string Text);
