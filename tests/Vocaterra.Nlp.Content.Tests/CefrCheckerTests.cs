using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for <see cref="CefrChecker"/>.</summary>
public class CefrCheckerTests
{
    private static DialogueCatalog Catalog(params (string Intent, string Text)[] lines)
    {
        return new DialogueCatalog(lines.Select(line => KeyValuePair.Create(
            line.Intent,
            new VariantPool(line.Intent, [new DialogueTemplate(line.Intent + ".1", line.Text)]))));
    }

    private static ImmutableSortedSet<string> Set(params string[] lemmas)
    {
        return ImmutableSortedSet.CreateRange(StringComparer.Ordinal, lemmas);
    }

    [Fact]
    public void ListsTheLemmasThatAreNotInTheList()
    {
        var catalog = Catalog(("offer", "Hola, ¿quieres una espada?"), ("bye", "Adiós, amigo."));

        var report = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("hola", "querer", "amigo"), "A1");

        Assert.Equal("a1", report.Level);
        Assert.Equal(["adiós", "espada", "un"], report.Missing.Select(missing => missing.Lemma));
        Assert.Equal(3, report.CoveredLemmas);
        Assert.Equal(6, report.TotalLemmas);
        Assert.All(report.Missing, missing => Assert.True(missing.InLexicon));
        Assert.Equal(["bye.1"], report.Missing[0].LineIds);
        Assert.Equal(["offer.1"], report.Missing[1].LineIds);
    }

    [Fact]
    public void PlaceholdersAndNumbersAreNotCounted()
    {
        var catalog = Catalog(("sell", "Vendo {qty} {item} por 5 monedas {{ya}}."));

        var report = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("vender", "por"), "a1");

        Assert.Equal(["moneda", "ya"], report.Missing.Select(missing => missing.Lemma));
        Assert.Equal(2, report.CoveredLemmas);
    }

    [Fact]
    public void WordsTheLexiconDoesNotKnowAreFlagged()
    {
        var catalog = Catalog(("odd", "Hola zzxq."));

        var report = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("hola"), "a1");

        var missing = Assert.Single(report.Missing);
        Assert.Equal("zzxq", missing.Lemma);
        Assert.False(missing.InLexicon);
        Assert.Equal(1, report.CoveredLemmas);
    }

    [Fact]
    public void AnyReadingInTheListCoversTheWord()
    {
        // "como" is both the conjunction/adverb "como" and a form of the verb "comer".
        var catalog = Catalog(("eat", "Como pan."));

        var asVerb = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("comer", "pan"), "a1");
        var asWord = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("como", "pan"), "a1");
        var neither = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("pan"), "a1");

        Assert.Empty(asVerb.Missing);
        Assert.Empty(asWord.Missing);
        Assert.Single(neither.Missing);
    }

    [Fact]
    public void ARepeatedLemmaIsCountedOnceWithAllItsLines()
    {
        var catalog = Catalog(("a", "Quiero espada."), ("b", "Una espada."));

        var report = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("querer"), "a1");

        var espada = report.Missing.Single(missing => missing.Lemma == "espada");
        Assert.Equal(["a.1", "b.1"], espada.LineIds);
    }

    [Fact]
    public void AnEmptyCatalogHasNothingToReport()
    {
        var report = CefrChecker.Check(DialogueCatalog.Empty, NlpLexicon.Seed, Set("hola"), "a1");

        Assert.Equal(0, report.TotalLemmas);
        Assert.Empty(report.Missing);
    }

    [Fact]
    public void ALevelIncludesTheLevelsBelowIt()
    {
        var cefr = new Dictionary<string, IReadOnlySet<string>>
        {
            ["a1"] = Set("hola"),
            ["a2"] = Set("espada"),
            ["b1"] = Set("dragón"),
        };

        Assert.Equal(["hola"], CefrChecker.AllowedLemmas(cefr, "a1"));
        Assert.Equal(["espada", "hola"], CefrChecker.AllowedLemmas(cefr, "A2"));
        Assert.Equal(["dragón", "espada", "hola"], CefrChecker.AllowedLemmas(cefr, "b1"));
        Assert.Throws<ArgumentException>(() => CefrChecker.AllowedLemmas(cefr, "c1"));
    }

    [Fact]
    public void TheSampleBundleIsCheckedAgainstTheBundleLists()
    {
        var a1 = CefrChecker.Check(TestSupport.Bundle, "a1");
        var a2 = CefrChecker.Check(TestSupport.Bundle, "a2");

        Assert.True(a1.TotalLemmas > 40);
        Assert.Equal(a1.TotalLemmas, a1.CoveredLemmas + a1.Missing.Count);
        Assert.Equal(a2.TotalLemmas, a2.CoveredLemmas + a2.Missing.Count);
        Assert.True(a1.CoveredLemmas > a1.Missing.Count);
        Assert.True(a2.Missing.Count < a1.Missing.Count);
        Assert.True(a2.CoveredLemmas > a1.CoveredLemmas);
        var herreria = a1.Missing.Single(missing => missing.Lemma == "herrería");
        Assert.True(herreria.InLexicon);
        Assert.Contains("greet_reply.1", herreria.LineIds);
        Assert.DoesNotContain(a1.Missing, missing => missing.Lemma == "hola");
        Assert.DoesNotContain(a1.Missing, missing => missing.Lemma == "ayudar");
        Assert.Equal(a1, CefrChecker.Check(TestSupport.Bundle, "A1"));
    }

    [Fact]
    public void AnUnknownLevelIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CefrChecker.Check(TestSupport.Bundle, "b2"));
    }

    [Fact]
    public void TheReportIsFormattedAsCountsAndMissingLemmas()
    {
        var report = new CefrReport(
            "a1",
            4,
            2,
            [new CefrMissingLemma("espada", true, ["a.1", "b.1"]), new CefrMissingLemma("zzxq", false, ["c.1"])]);

        Assert.Equal(
            "cefr a1: 4 lemmas in dialogue templates, 2 covered, 2 not in the list\n  espada: a.1, b.1\n  zzxq (not in lexicon): c.1\n",
            CefrChecker.Format(report));
    }

    private static PromptSpec Prompt(string id, string? text)
    {
        return new PromptSpec(id, RecognitionMode.Open, Difficulty.Beginner, "items-basic", [], null, [], text);
    }

    private static Dictionary<string, PromptSpec> Prompts(params PromptSpec[] prompts)
    {
        return prompts.ToDictionary(prompt => prompt.PromptId, StringComparer.Ordinal);
    }

    [Fact]
    public void PromptTextIsCheckedAndReportedWithItsPromptId()
    {
        var prompts = Prompts(
            Prompt("zeta", "Tengo una espada."),
            Prompt("alpha", "Hola, amigo. ¿Quieres una espada?"),
            Prompt("silent", null));

        var report = CefrChecker.Check(DialogueCatalog.Empty, NlpLexicon.Seed, Set("hola", "amigo"), "a1", prompts);

        Assert.NotNull(report.Prompts);
        Assert.Equal(["espada", "querer", "tener", "un"], report.Prompts.Missing.Select(missing => missing.Lemma));
        Assert.All(report.Prompts.Missing, missing => Assert.Equal(missing.LineIds.Count, missing.LineIds.Distinct().Count()));
        Assert.Equal(["alpha", "zeta"], report.Prompts.Missing.Single(missing => missing.Lemma == "espada").LineIds);
        Assert.Equal(["alpha"], report.Prompts.Missing.Single(missing => missing.Lemma == "querer").LineIds);
        Assert.Equal(["zeta"], report.Prompts.Missing.Single(missing => missing.Lemma == "tener").LineIds);
        Assert.Equal(2, report.Prompts.CoveredLemmas);
        Assert.Equal(report.Prompts.CoveredLemmas + report.Prompts.Missing.Count, report.Prompts.TotalLemmas);
        Assert.Empty(report.Missing);
    }

    [Fact]
    public void PromptFindingsDoNotChangeTheDialogueCounts()
    {
        var catalog = Catalog(("offer", "Hola, ¿quieres una espada?"));

        var without = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("hola"), "a1");
        var with = CefrChecker.Check(catalog, NlpLexicon.Seed, Set("hola"), "a1", Prompts(Prompt("p", "Adiós.")));

        Assert.Null(without.Prompts);
        Assert.Equal(without.TotalLemmas, with.TotalLemmas);
        Assert.Equal(without.Missing, with.Missing);
        Assert.Equal(["adiós"], with.Prompts!.Missing.Select(missing => missing.Lemma));
    }

    [Fact]
    public void ANullPromptListLeavesThePromptFindingsOut()
    {
        var report = CefrChecker.Check(DialogueCatalog.Empty, NlpLexicon.Seed, Set("hola"), "a1", null);

        Assert.Null(report.Prompts);
        Assert.Equal(report, CefrChecker.Check(DialogueCatalog.Empty, NlpLexicon.Seed, Set("hola"), "a1"));
    }

    [Fact]
    public void PromptTextIsAnalyzedAsWrittenAndIndefiniteArticlesAreKept()
    {
        var prompts = Prompts(Prompt("p", "Vendo un oro."));

        var report = CefrChecker.Check(DialogueCatalog.Empty, NlpLexicon.Seed, Set("vender", "oro"), "a1", prompts);

        Assert.Equal(["un"], report.Prompts!.Missing.Select(missing => missing.Lemma));
        Assert.Equal(["p"], report.Prompts.Missing[0].LineIds);
    }

    [Fact]
    public void TheFormatListsPromptFindingsAfterTheDialogueFindings()
    {
        var report = new CefrReport(
            "a1",
            1,
            0,
            [new CefrMissingLemma("espada", true, ["a.1"])],
            new CefrPromptReport(2, 1, [new CefrMissingLemma("tienda", true, ["offer-items-open"])]));

        Assert.Equal(
            "cefr a1: 1 lemmas in dialogue templates, 0 covered, 1 not in the list\n  espada: a.1\n"
            + "cefr a1: 2 lemmas in prompt text, 1 covered, 1 not in the list\n  tienda: offer-items-open\n",
            CefrChecker.Format(report));
    }

    [Fact]
    public void ReportsWithPromptFindingsCompareByValue()
    {
        var one = new CefrReport("a1", 0, 0, [], new CefrPromptReport(1, 0, [new CefrMissingLemma("x", true, ["p"])]));
        var two = new CefrReport("a1", 0, 0, [], new CefrPromptReport(1, 0, [new CefrMissingLemma("x", true, ["p"])]));

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.NotEqual(one, new CefrReport("a1", 0, 0, []));
        Assert.NotEqual(one, new CefrReport("a1", 0, 0, [], new CefrPromptReport(1, 0, [new CefrMissingLemma("y", true, ["p"])])));
    }

    [Fact]
    public void TheSampleBundlesPromptTextIsCheckedWithTheDialogueTemplates()
    {
        var a1 = CefrChecker.Check(TestSupport.Bundle, "a1");

        Assert.NotNull(a1.Prompts);
        Assert.True(a1.Prompts.TotalLemmas > 0);
        Assert.Equal(a1.Prompts.TotalLemmas, a1.Prompts.CoveredLemmas + a1.Prompts.Missing.Count);
        Assert.All(a1.Prompts.Missing, missing => Assert.All(missing.LineIds, id => Assert.Contains(id, TestSupport.Bundle.Prompts.Keys)));
        Assert.Equal(a1, CefrChecker.Check(TestSupport.Bundle, "a1"));
    }

    [Fact]
    public void ReportsCompareByValue()
    {
        var one = new CefrReport("a1", 1, 0, [new CefrMissingLemma("x", true, ["a.1"])]);
        var two = new CefrReport("a1", 1, 0, [new CefrMissingLemma("x", true, ["a.1"])]);

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.NotEqual(one, new CefrReport("a1", 1, 0, [new CefrMissingLemma("y", true, ["a.1"])]));
    }
}
