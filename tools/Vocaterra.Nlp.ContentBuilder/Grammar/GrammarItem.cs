namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>How a string is written. Learner-side strings are stored in lower case (the core normalises ASR text), so they are capitalised before a check.</summary>
internal enum GrammarKind
{
    /// <summary>Authored speech of the host or the NPC, checked as written.</summary>
    Npc,

    /// <summary>What a learner may say (answer aliases, intent examples, phrase variants); the first letter is capitalised before the check.</summary>
    Learner,
}

/// <summary>
/// One string to check. <see cref="Original"/> is the text as authored (or, for a rendered slot template, the rendered text); <see cref="Filling"/> is the
/// slot filling that produced it (empty for a plain string). An <see cref="IsExample"/> string is a learner example: it is graded by the learner-error
/// oracle against its <see cref="ExpectedErrors"/> (none when it has no tags), instead of producing one finding per LanguageTool match.
/// </summary>
internal sealed record GrammarItem(
    string Id,
    string Path,
    string Original,
    GrammarKind Kind,
    string Filling,
    IReadOnlyList<string>? ExpectedErrors,
    bool Example = false)
{
    /// <summary>The text sent to LanguageTool: the original, with the first letter capitalised for learner-side strings.</summary>
    public string CheckText => Kind == GrammarKind.Learner ? GrammarStrings.Capitalise(Original) : Original;

    /// <summary>The ids a finding names: the string id, then the filling when the string was rendered from a template.</summary>
    public IReadOnlyList<string> Ids => Filling.Length == 0 ? [Id] : [Id, Filling];

    /// <summary>True when the string is a learner example (graded by the oracle).</summary>
    public bool IsExample => Example || ExpectedErrors is not null;
}

/// <summary>A malformed input (a bad JSONL line, a template that does not render, a waiver without a reason). The command exits 1 with the message.</summary>
internal sealed class GrammarInputException : Exception
{
    /// <summary>Creates the exception with the message the user sees.</summary>
    public GrammarInputException(string message)
        : base(message)
    {
    }
}
