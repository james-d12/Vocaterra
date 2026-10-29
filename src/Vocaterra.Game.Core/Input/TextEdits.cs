namespace Vocaterra.Game.Core.Input;

/// <summary>The editing keys of a text box held this frame, after their repeats: Backspace, Delete and the two arrows.</summary>
/// <param name="Backspace">Backspace fires.</param>
/// <param name="Delete">Delete fires.</param>
/// <param name="Left">Left fires.</param>
/// <param name="Right">Right fires.</param>
public readonly record struct TextEdits(bool Backspace, bool Delete, bool Left, bool Right);
