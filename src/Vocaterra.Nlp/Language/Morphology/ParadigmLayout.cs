namespace Vocaterra.Nlp.Language.Morphology;

/// <summary>One person of a conjugation table: the core's person (<c>1</c> to <c>3</c>) and number (<c>sg</c> or <c>pl</c>), as <see cref="IMorphology.Conjugate"/> takes them.</summary>
/// <param name="Person">The person.</param>
/// <param name="Number">The number.</param>
public sealed record ParadigmPerson(string Person, string Number);

/// <summary>
/// The shape of the conjugation table a language shows (M87): the persons of its rows and the tenses of its columns, both supplied by the language's pack, so a host
/// never hard-codes the persons of one language (Spanish has no <c>vosotros</c> row in the game; German has <c>ihr</c> and a formal <c>Sie</c> that shares the third plural).
/// The labels (the pronouns) are not part of it: they are the host's words.
/// </summary>
/// <param name="Persons">The persons of the rows, in order.</param>
/// <param name="Tenses">The tenses of the columns the morphology generates, in order (core tense ids such as <c>pres</c>, <c>pret</c> and <c>fut</c>).</param>
public sealed record ParadigmLayout(IReadOnlyList<ParadigmPerson> Persons, IReadOnlyList<string> Tenses)
{
    /// <summary>
    /// The layout a morphology has when its pack says nothing: the six persons (first to third, singular and plural) and the present tense alone.
    /// </summary>
    public static ParadigmLayout Default { get; } = new(
        [
            new("1", "sg"),
            new("2", "sg"),
            new("3", "sg"),
            new("1", "pl"),
            new("2", "pl"),
            new("3", "pl"),
        ],
        ["pres"]);
}

/// <summary>One cell of a paradigm: a person in a tense and the form the morphology generates for it.</summary>
/// <param name="Person">The person and number.</param>
/// <param name="Tense">The tense.</param>
/// <param name="Form">The form, or null when the morphology cannot generate it.</param>
public sealed record ParadigmCell(ParadigmPerson Person, string Tense, string? Form);
