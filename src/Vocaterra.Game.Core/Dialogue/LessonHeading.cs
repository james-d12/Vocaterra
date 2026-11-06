namespace Vocaterra.Game.Core.Dialogue;

/// <summary>
/// The lesson heading in the dialogue panel's header (U02, U02b): the lead word (<c>Lesson</c>, empty for the warm-up), the lesson's title, and the step's
/// count and kind (<c>step 3 of 25 · Say it</c>, empty for a lesson on offer). <see cref="Full"/> is the heading as one line.
/// </summary>
public readonly record struct LessonHeading(string Lead, string Title, string Tail)
{
    private const string Separator = " · ";

    /// <summary>The heading as one line, the parts that are not empty joined with <c>·</c>.</summary>
    public string Full => Join(Lead, Title, Tail);

    /// <summary>
    /// The heading fitted to <paramref name="maxWidth"/> (U02b): the title is cut with an ellipsis first, so the step and its kind stay; the lead word goes
    /// only when the title would have no room left; the whole line is cut only when even the step does not fit.
    /// </summary>
    /// <param name="maxWidth">The room the heading has.</param>
    /// <param name="measure">The width of a text.</param>
    public string FitTo(float maxWidth, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (measure(Full) <= maxWidth)
        {
            return Full;
        }

        if (Tail.Length > 0 && Title.Length > 0)
        {
            var tailWidth = measure(Separator + Tail);
            var leadPart = Lead.Length > 0 ? Lead + Separator : string.Empty;
            foreach (var lead in Lead.Length > 0 ? new[] { leadPart, string.Empty } : new[] { string.Empty })
            {
                var titleRoom = maxWidth - measure(lead) - tailWidth;
                if (titleRoom >= measure("…"))
                {
                    return lead + TextWrap.Ellipsize(Title, titleRoom, measure) + Separator + Tail;
                }
            }
        }

        return TextWrap.Ellipsize(Full, maxWidth, measure);
    }

    private static string Join(params string[] parts) => string.Join(Separator, parts.Where(static part => part.Length > 0));
}
