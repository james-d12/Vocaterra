namespace Vocaterra.Nlp.Es.Text;

/// <summary>The Spanish pack's <see cref="IOrthographyNotes"/> (M87): a wrapper that returns exactly what <see cref="SpanishAccentNotes.Describe"/> returns.</summary>
public sealed class SpanishOrthographyNotes : IOrthographyNotes
{
    /// <inheritdoc />
    public string Describe(AccentNote note) => SpanishAccentNotes.Describe(note);
}
