using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.Input;

/// <summary>The editing keys of a text box held this frame, after their repeats: Backspace, Delete and the two arrows.</summary>
/// <param name="Backspace">Backspace fires.</param>
/// <param name="Delete">Delete fires.</param>
/// <param name="Left">Left fires.</param>
/// <param name="Right">Right fires.</param>
public readonly record struct TextEdits(bool Backspace, bool Delete, bool Left, bool Right);

/// <summary>The repeats of the text boxes' editing keys (<see cref="KeyRepeater"/>), one set for the dialogue's and the report's text boxes: only one is open at a time.</summary>
public sealed class TextEditKeys
{
    private readonly KeyRepeater _backspace = new();
    private readonly KeyRepeater _delete = new();
    private readonly KeyRepeater _left = new();
    private readonly KeyRepeater _right = new();

    /// <summary>One frame of the editing keys: each one fires when it goes down and again while it is held, at the repeat's rate.</summary>
    public TextEdits Step(IInputFrame keys) =>
        new(_backspace.Step(keys.Down(GameKey.Back)), _delete.Step(keys.Down(GameKey.Delete)), _left.Step(keys.Down(GameKey.Left)), _right.Step(keys.Down(GameKey.Right)));
}
