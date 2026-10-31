namespace Vocaterra.Game.Core.Tip;

/// <summary>One piece of the objective line: its text, what it is, and whether its step is done.</summary>
/// <param name="Text">The text, including any space before it.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Done">Whether the step it belongs to is done.</param>
public sealed record ObjectivePiece(string Text, ObjectivePieceKind Kind, bool Done = false);
