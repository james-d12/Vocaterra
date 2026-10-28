namespace Vocaterra.Game.UI;

/// <summary>One of the five fixed speech buttons: what it does, what it says and its key.</summary>
/// <param name="Action">What it does.</param>
/// <param name="Label">The Spanish label.</param>
/// <param name="Key">The key letter shown before the label.</param>
public sealed record SpeechButton(SpeechAction Action, string Label, string Key)
{
    /// <summary>The text drawn on the button, for example <c>[R] Repetir</c>.</summary>
    public string Text => $"[{Key}] {Label}";
}
