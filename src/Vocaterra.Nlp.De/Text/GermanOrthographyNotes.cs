namespace Vocaterra.Nlp.De.Text;

/// <summary>
/// The German pack's <see cref="IOrthographyNotes"/> (M37). German analysis raises no <see cref="AccentNote"/> (umlauts are letters, restored by the recast
/// policy, not accents that mark a tense), so this is a placeholder with a plain wording until a milestone gives German notes a use.
/// </summary>
public sealed class GermanOrthographyNotes : IOrthographyNotes
{
    /// <inheritdoc />
    public string Describe(AccentNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return $"„{note.Accented}“ mit Umlaut oder Akzent";
    }
}
