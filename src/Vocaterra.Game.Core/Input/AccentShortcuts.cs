namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The Spanish letters and marks for a keyboard that cannot type them: with Alt held, a vowel gives the same vowel with an acute accent,
/// <c>N</c> gives <c>ñ</c>, <c>/</c> (the question mark key) gives <c>¿</c> and <c>1</c> (the exclamation mark key) gives <c>¡</c>. Shift gives
/// the capital. The window's own text events still deliver the letters a layout or a compose key makes.
/// </summary>
public static class AccentShortcuts
{
    /// <summary>
    /// What a typed character becomes: with Alt held, the accented letter Alt gives (<see cref="Map"/>), otherwise the character itself. The
    /// window's text event is the one place that sees the letters, so the dialogue and the report both go through here (S21 found the Alt keys were
    /// shown in the hints but never applied).
    /// </summary>
    /// <param name="typed">The character the platform typed.</param>
    /// <param name="alt">Whether Alt is held.</param>
    public static char ForTyped(char typed, bool alt) => alt && Map(typed, char.IsUpper(typed)) is { } accent ? accent : typed;

    /// <summary>The character for Alt plus <paramref name="key"/> (a lower-case letter, <c>/</c> or <c>1</c>), or <see langword="null"/>.</summary>
    /// <param name="key">The key, as the character it types without modifiers.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static char? Map(char key, bool shift)
    {
        char? lower = char.ToLowerInvariant(key) switch
        {
            'a' => 'á',
            'e' => 'é',
            'i' => 'í',
            'o' => 'ó',
            'u' => 'ú',
            'n' => 'ñ',
            _ => null,
        };
        if (lower is { } letter)
        {
            return shift ? char.ToUpperInvariant(letter) : letter;
        }

        return key switch
        {
            '/' or '?' => '¿',
            '1' or '!' => '¡',
            _ => null,
        };
    }
}
