using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es.Text;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M70: the lines of the QA pass on the village (<c>docs/qa-reports/2026-11-01-language.md</c>) and of the owner's answers of 2026-11-01 (D-game-22, items 2 and 3),
/// through the whole pipeline at the game's prompts, as the CLI and the host grade them. Each failed before M70; the correct neighbours are tests as well.
/// </summary>
public class VillageQaLineTests
{
    private static readonly Lazy<ContentBundle> NameBundle = new(BuildNameBundle);

    private static PipelineResult Evaluate(string transcript, string promptId, ContentBundle? bundle = null)
    {
        var content = bundle ?? TestSupport.Bundle;
        var prompt = content.Prompts[promptId];
        return TestSupport.Pipeline(transcript, prompt.Mode, bundle: content).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(agentId: prompt.AgentId ?? "player"));
    }

    private static string Notes(PipelineResult result)
    {
        return string.Join(';', (result.Notes ?? []).Select(static n => $"{n.Written}|{n.Accented}|{n.Marks}"));
    }

    private static string Recast(PipelineResult result)
    {
        return Assert.Single(result.Response.Lines, static line => line.IsRecast).Text;
    }

    // The change for the game's V21 (QA-language-2): the intent ask_name binds a free name after me llamo. The bundle as it is has no such grammar.
    private static ContentBundle BuildNameBundle()
    {
        using var copy = new TempBundle(rehash: false);
        copy.EditJson(
            "intents/intents.json",
            root =>
            {
                foreach (var intent in root["intents"]!.AsArray())
                {
                    if ((string?)intent!["intentId"] == "ask_name")
                    {
                        intent["slotGrammars"]!.AsArray().Add("me [VERB:llamar] [WORD:name]");
                    }
                }
            });
        return copy.Load();
    }

    private static string Errors(Verdict verdict)
    {
        return string.Join(';', verdict.Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // ---- 1. A typed n for ñ (QA-language-7; D-game-22, item 2) ---------------------------------------------------------------------------------------------

    // Before M70: vocab.unknown on manana, correctness 0.6 at the lessons, and no intent at lesson-will-sell on the CLI.
    [Theory]
    [InlineData("manana compraré pan", "lesson-will-buy")]
    [InlineData("mañana compraré pan", "lesson-will-buy")]
    [InlineData("manana venderé fruta", "lesson-will-sell")]
    [InlineData("mañana venderé fruta", "lesson-will-sell")]
    public void ATypedNForNTildeIsNoErrorAndKeepsTheIntent(string transcript, string promptId)
    {
        var verdict = Evaluate(transcript, promptId).Verdict;

        Assert.Equal(string.Empty, Errors(verdict));
        Assert.Equal(1.0, verdict.Correctness);
        Assert.True(verdict.Communicated);
        Assert.Equal("state_action", verdict.Intent.Value);
    }

    [Fact]
    public void AWordOfItsOwnSpelledWithNIsGradedAsBefore()
    {
        Assert.Equal("vocab.unknown|anos|", Errors(Evaluate("tengo diez anos", "villager-open").Verdict));
    }

    // ---- 2. An accent that marks a verb's tense (D-game-22, item 3) ---------------------------------------------------------------------------------------------

    // Accepted with no deduction, as before M70, and now with a note that names the accented form (before M70: no note; ayer compre pan gave conj.tense compré).
    [Theory]
    [InlineData("compre dos panes", "lesson-bought", "compre|compré|Tense")]
    [InlineData("compre tres manzanas", "lesson-market", "compre|compré|Tense")]
    [InlineData("luz compro pescado", "lesson-luz-bought", "compro|compró|Tense")]
    [InlineData("mar pesco en el río", "lesson-mar-fished", "pesco|pescó|Tense")]
    [InlineData("ayer compre pan", "villager-open", "compre|compré|Tense")]
    public void AVerbWithoutTheAccentOfItsTenseIsAcceptedWithANote(string transcript, string promptId, string notes)
    {
        var result = Evaluate(transcript, promptId);

        Assert.Equal(string.Empty, Errors(result.Verdict));
        Assert.Equal(1.0, result.Verdict.Correctness);
        Assert.Equal(notes, Notes(result));
    }

    [Theory]
    [InlineData("compré dos panes", "lesson-bought")]
    [InlineData("luz compró pescado", "lesson-luz-bought")]
    [InlineData("yo compro pan", "villager-open")]
    [InlineData("quiero que compre pan", "villager-open")]
    [InlineData("compre tres manzanas", "villager-open")]
    [InlineData("hola luz quiero dos panes", "baker-open")]
    public void ALineWithNothingToNoteHasNoNotes(string transcript, string promptId)
    {
        Assert.Null(Evaluate(transcript, promptId).Notes);
    }

    [Fact]
    public void TheNoteIsOnTheTwoPhaseAnalysisAndHasItsSpanishLine()
    {
        var prompt = TestSupport.Bundle.Prompts["lesson-bought"];
        var analysis = TestSupport.Pipeline("compre dos panes", prompt.Mode).Analyze(TestSupport.Clip(), prompt);
        var note = Assert.Single(analysis.Notes!);

        Assert.Equal(new TextSpan(0, 6), note.Span);
        Assert.Equal("comprar", note.Reading.Lemma);
        Assert.Equal("«compré», con tilde: es pasado", SpanishAccentNotes.Describe(note));
        Assert.DoesNotContain("notes", TestSupport.Json(Evaluate("compré dos panes", "lesson-bought")), StringComparison.Ordinal);
    }

    // ---- 3. gustar agrees with the thing liked (QA-language-4) ------------------------------------------------------------------------------------------------

    // Before M70 both were state_like at correctness 1, and the NPC praised them.
    [Theory]
    [InlineData("yo gusto el pan", "lesson-like-bread", "conj.person|yo gusto|me gusta", "me gusta el pan")]
    [InlineData("me gusto el pan", "lesson-like-bread", "conj.person|gusto|gusta", "me gusta el pan")]
    [InlineData("yo gusto el pan", "farmer-open", "conj.person|yo gusto|me gusta", "me gusta el pan")]
    [InlineData("me gusta las manzanas", "lesson-like-apples", "conj.person|gusta|gustan", "me gustan las manzanas")]
    public void AWrongGustarIsAPersonErrorAndTheRecastSaysTheDative(string transcript, string promptId, string errors, string recast)
    {
        var result = Evaluate(transcript, promptId);

        Assert.Equal(errors, Errors(result.Verdict));
        Assert.Equal("state_like", result.Verdict.Intent.Value);
        Assert.Contains("«" + recast + "»", Recast(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("me gusta el pan", "lesson-like-bread")]
    [InlineData("te gusta el pan", "lesson-like-bread")]
    [InlineData("a mí me gusta el pan", "lesson-like-bread")]
    [InlineData("no me gusta", "lesson-like-bread")]
    [InlineData("me gustan las manzanas", "lesson-like-apples")]
    public void ARightGustarIsClean(string transcript, string promptId)
    {
        Assert.Equal(string.Empty, Errors(Evaluate(transcript, promptId).Verdict));
    }

    // ---- 4. An unknown word is blamed on itself (QA-language-6) -----------------------------------------------------------------------------------------------

    // Before M70: compré three manzanas gave vocab.unknown expected compré and the recast «compré compré manzanas»; mañana iré al glorp gave expected iré and
    // «mañana iré al iré». The recast is the core's (RecastBuilder); the line around it is the bundle's dialogue.
    [Theory]
    [InlineData("compré three manzanas", "lesson-market", "vocab.unknown|three|tres", "compré tres manzanas")]
    [InlineData("mañana iré al glorp", "lesson-will-go", "vocab.unknown|glorp|río", "mañana iré al río")]
    public void TheUnknownWordIsNamedAndTakesTheAnswersWordAtItsPlace(string transcript, string promptId, string errors, string recast)
    {
        var result = Evaluate(transcript, promptId);

        Assert.Equal(errors, Errors(result.Verdict));
        Assert.Contains("«" + recast + "»", Recast(result), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownWordTheAnswerHasNoPlaceForGetsNoWord()
    {
        Assert.Equal("vocab.unknown|glorp|", Errors(Evaluate("glorp iré al río", "lesson-will-go").Verdict));
    }

    // ---- 5. The preterite's spelling changes (QA-language-9) --------------------------------------------------------------------------------------------------

    // Before M70: vocab.unknown with the wrong expected word (buscaré, pescó), no intent, and the NPC said both «buscaré madera» and No te entendí.
    [Theory]
    [InlineData("buscé madera", "lesson-will-fetch", "conj.irregular|buscé|busqué")]
    [InlineData("mar pescé en el río", "lesson-mar-fished", "conj.irregular|pescé|pesqué")]
    [InlineData("ayer llegé al río", "villager-open", "conj.irregular|llegé|llegué")]
    public void TheSpellingSlipIsTheVerbsSlip(string transcript, string promptId, string errors)
    {
        Assert.Equal(errors, Errors(Evaluate(transcript, promptId).Verdict));
    }

    // ---- 6. A learner's own name (QA-language-2) --------------------------------------------------------------------------------------------------------------

    // The cause is the bundle: no grammar of ask_name binds a name after me llamo (the keyword rule llamar understood the line and ana stayed an unknown word). With
    // the grammar V21 adds, the core binds any word of letters as the name: no error, the name in the slot, and no recast that drops it.
    [Theory]
    [InlineData("me llamo ana", "farmer-open", "ana")]
    [InlineData("me llamo luis", "smith-open", "luis")]
    [InlineData("me llamo domingo", "smith-open", "domingo")]
    [InlineData("me llamo marta", "farmer-open", "marta")]
    public void WithANameSlotTheLearnersNameIsAName(string transcript, string promptId, string name)
    {
        var result = Evaluate(transcript, promptId, NameBundle.Value);

        Assert.Equal(string.Empty, Errors(result.Verdict));
        Assert.True(result.Verdict.Communicated);
        Assert.Equal("ask_name", result.Verdict.Intent.Value);
        Assert.Equal(name, result.Verdict.Slots["name"]);
        Assert.DoesNotContain(result.Response.Lines, static line => line.IsRecast);
    }

    [Fact]
    public void WithoutTheNameSlotTheNameIsAnUnknownWordAsTheQaPassFound()
    {
        Assert.Equal("vocab.unknown|ana|", Errors(Evaluate("me llamo ana", "farmer-open").Verdict));
    }

    // ---- 7. The learner's own word in an error (an observation) -----------------------------------------------------------------------------------------------

    // Before M70: heard '2'.
    [Theory]
    [InlineData("dos panes es caro", "dos")]
    [InlineData("Dos panes son caros", "dos")]
    public void AnErrorOnANumberReportsTheLearnersWord(string transcript, string heard)
    {
        var error = Assert.Single(Evaluate(transcript, "farmer-open").Verdict.Errors);

        Assert.Equal("vocab.wrong", error.Tag);
        Assert.Equal(heard, error.Actual);
    }
}
