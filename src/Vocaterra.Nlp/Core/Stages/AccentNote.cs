using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>What the written accent of an <see cref="AccentNote"/> tells apart (M70).</summary>
public enum AccentMark
{
    /// <summary>The tense or the mood: <c>compré</c>, the preterite, beside <c>compre</c>, the present subjunctive; <c>compró</c> beside <c>compro</c>, the present.</summary>
    Tense,

    /// <summary>Only the person or the number, in the same tense and mood.</summary>
    Person,
}

/// <summary>
/// A verb the learner wrote without the accent that marks its tense or person, read as the accented form (M70; the owner's answer of 2026-11-01, D-game-22 item 3).
/// It is no error and costs nothing; a host shows the accented form with a short note, so the learner sees why the accent matters.
/// </summary>
/// <remarks>
/// The analysis makes one where the written form has a tense error that the accented form of the same person takes away (<c>ayer compre pan</c> is read as
/// <c>compré</c>; a person error stays one, <c>ella compro pan</c> giving <c>compra</c>); the pipeline makes one where the line matched an answer whose word at that
/// place is the accented form of the same verb (<c>luz compro pescado</c> for the answer <c>luz compró pescado</c>, <c>compre dos panes</c> for <c>compré dos
/// panes</c>). A word without the accent that is the same form (<c>comi</c> for <c>comí</c>, which is no other word) and a word that is not a verb (<c>esta</c> for
/// <c>está</c>, <c>el</c> for <c>él</c>) get none: their accent tells no tense or person apart.
/// </remarks>
/// <param name="Span">The word in the normalized text.</param>
/// <param name="Written">The word as written, normalized (<c>compre</c>).</param>
/// <param name="Accented">The accented form the word was read as (<c>compré</c>).</param>
/// <param name="Marks">What the accent tells apart: <see cref="AccentMark.Tense"/> when no reading of the written word has the tense and mood of the accented form, else <see cref="AccentMark.Person"/>.</param>
/// <param name="Reading">The reading of the accented form: its lemma, person, number, tense and mood (<c>comprar</c>, 1, singular, <c>pret</c>, <c>ind</c>).</param>
public sealed record AccentNote(TextSpan Span, string Written, string Accented, AccentMark Marks, LexicalFeatures Reading);
