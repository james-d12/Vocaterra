namespace Vocaterra.Nlp.De.Text;

/// <summary>
/// The text M38 to M40 will fill: the German morphology, analysis rules and template functions are not written yet. The pack's factory methods throw this
/// exception, so a German bundle fails to load with a clear message instead of running on a wrong morphology.
/// </summary>
internal static class GermanPlaceholders
{
    public static NotSupportedException NotYet(string what, string milestone) =>
        new($"The German {what} is not written yet ({milestone}); the German pack (M37) has the text layer only.");
}
