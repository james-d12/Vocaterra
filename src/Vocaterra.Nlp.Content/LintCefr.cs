namespace Vocaterra.Nlp.Content;

/// <summary>Check 5: the lemmas of dialogue templates, prompt text and answer-set canonicals above the requested CEFR level.</summary>
internal static class LintCefr
{
    public static void Run(LintContext context)
    {
        if (context.Options.Level is not { } requested)
        {
            return;
        }

        var level = requested.ToLowerInvariant();
        var bundle = context.Bundle;
        var severity = context.Options.FailOnLevel ? LintSeverity.Error : LintSeverity.Warning;
        var report = CefrChecker.Check(bundle, level);
        Report(context, severity, "cefr.dialogue", ContentPaths.Dialogue, level, "used by the dialogue lines", report.Missing);
        if (report.Prompts is { } prompts)
        {
            Report(context, severity, "cefr.prompt", ContentPaths.Prompts, level, "used by the text of the prompts", prompts.Missing);
        }

        Report(context, severity, "cefr.answer", ContentPaths.AnswerSetFolder, level, "used by the canonical answers of the answer sets", CefrChecker.CheckAnswerSets(bundle, level).Missing);
    }

    private static void Report(
        LintContext context,
        LintSeverity severity,
        string checkId,
        string path,
        string level,
        string where,
        IReadOnlyList<CefrMissingLemma> missing)
    {
        foreach (var lemma in missing)
        {
            var suffix = lemma.InLexicon ? string.Empty : " and not in the lexicon";
            context.Add(
                severity,
                checkId,
                path,
                lemma.LineIds,
                $"lemma '{lemma.Lemma}' is not in the {level.ToUpperInvariant()} list{suffix} ({where} in the ids).");
        }
    }
}
