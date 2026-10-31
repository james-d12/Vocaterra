using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Es.Normalization;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Host.Tests;

/// <summary>
/// The village's talk on the host's path (<c>--transcript … --prompt …</c>, VILLAGE_PLAN V14): a statement at the smith's open prompt, <c>tener que</c> against
/// <c>tener</c> with a noun, and the listener's name said as a vocative, which the host reads as the game does (<see cref="Vocative"/>).
/// </summary>
public sealed class VillageTalkTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    private static readonly Dictionary<string, string> Names = NpcRoster.Load(GameContent.Bundle).All
        .Where(static npc => npc.Name is not null)
        .ToDictionary(static npc => npc.Id, static npc => npc.Name!, StringComparer.Ordinal);

    private static Nlp.Pipeline.PipelineResult Evaluate(string line, string prompt, bool names = true) =>
        new ConversationSession(GameContent.Bundle, listenerNames: names ? Names : null).Evaluate(line, prompt, Now);

    [Theory]
    [InlineData("domingo hizo un hacha")] // the orchestrator's case: ask_activity with vocab.wrong before V14
    [InlineData("hizo un hacha")]
    [InlineData("domingo trabaja en la herrería")]
    [InlineData("luz hizo pan")]
    [InlineData("ellos comieron pan")]
    public void AStatementAtTheSmithsOpenPromptIsAStatementAndErrorFree(string line)
    {
        // smith-open takes state_action since V14 (his agent answers it as before), and his answer set holds the day's statements, so the core finds no wrong word.
        var verdict = Evaluate(line, "smith-open", names: false).Verdict;

        Assert.Equal(("state_action", true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Empty(verdict.Errors);
    }

    [Theory]
    [InlineData("tengo que ir al río", "state_obligation")]
    [InlineData("tengo que ir a la panadería", "state_obligation")]
    [InlineData("tengo que comprar pan", "state_obligation")]
    [InlineData("hoy tengo que ir rápido al río", "state_obligation")] // no grammar matches: the keyword rule, tener with que (two lemmas), beats state_have's tener with a noun
    [InlineData("tengo dos panes", "state_have")]
    [InlineData("tengo madera", "state_have")]
    [InlineData("tengo una botella", "state_have")]
    public void TenerQueIsAnObligationAndTenerWithANounAPossessionWhereBothAreAllowed(string text, string intent)
    {
        var bundle = GameContent.Bundle;
        var normalized = SpanishTextNormalizer.Normalize(text, new NormalizationOptions(ConvertIndefiniteArticles: false));
        var analysis = new LinguisticAnalyzer(bundle.Lexicon, SpanishLanguagePack.Instance).Analyze(normalized, bundle.Intents.Grammars);
        var prompt = new PromptSpec("probe", RecognitionMode.Open, Difficulty.Beginner, bundle.AnswerSets.Keys.First(), ["state_have", "state_obligation"], null, []);

        var resolution = new IntentResolver(bundle.Intents, SpanishLanguagePack.Instance).Resolve(normalized, analysis, prompt);

        Assert.Equal(intent, resolution.Intent.Value);
    }

    [Fact]
    public void TheHostDemoOfTheObligationAndOfTheFishersPastIsUnderstood()
    {
        var must = Evaluate("tengo que ir a la panadería", "lesson-must-go").Verdict; // the gate's demo (the step's answer is the river: vocab.wrong names the place)
        Assert.Equal(("state_obligation", true), (must.Intent.Value, must.Communicated));

        var fished = Evaluate("Mar pescó en el río", "lesson-mar-fished").Verdict;
        Assert.Equal(("state_action", true), (fished.Intent.Value, fished.Communicated));
        Assert.Empty(fished.Errors);
    }

    [Theory]
    [InlineData("baker-open", "Luz, quiero pan", "request_item", "pan")]
    [InlineData("baker-open", "quiero pan, Luz", "request_item", "pan")]
    [InlineData("baker-open", "Luz quiero dos panes", "request_item", "pan")]
    [InlineData("fisher-open", "Mar, ¿dónde está la panadería?", "ask_directions", "panadería")]
    [InlineData("fisher-open", "¿dónde está la herrería, Mar?", "ask_directions", "herrería")]
    [InlineData("smith-open", "Domingo, necesito un hacha", "state_want", "hacha")]
    [InlineData("smith-open", "Domingo, ¿cómo te llamas?", "ask_name", null)]
    [InlineData("farmer-open", "León, quiero tres manzanas rojas", "request_item", "manzana")]
    [InlineData("lesson-they-ate", "Domingo, ellos comieron pan", "state_action", "pan")]
    [InlineData("lesson-luz-bought", "Mar, Luz compró pescado", "state_action", "pescado")]
    public void TheListenersNameSaidAsAVocativeIsReadAsTheLineWithoutIt(string prompt, string line, string intent, string? slot)
    {
        var verdict = Evaluate(line, prompt).Verdict;

        Assert.Equal((intent, true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Empty(verdict.Errors);
        if (slot is not null)
        {
            Assert.Contains(slot, verdict.Slots.Values);
        }
    }

    [Fact]
    public void WithoutTheNamesTheCoreReadsTheVocativeAsAWordOfTheLine()
    {
        // Before V14: the baker's name before a request resolved no intent, and the fisher's was read as the sea, the place asked about.
        Assert.Equal("unknown", Evaluate("Luz quiero pan", "baker-open", names: false).Verdict.Intent.Value);
        Assert.Equal("mar", Evaluate("Mar dónde está la panadería", "fisher-open", names: false).Verdict.Slots["place"]);

        // A name that is no vocative stays: another NPC's name, a name joined to another, and a name a preposition governs.
        Assert.Equal("luz", Evaluate("luz y mar comieron pan", "lesson-they-ate").Verdict.Slots["item"]); // the keyword rule's slot, as before: the names are the subject
        Assert.Equal(("state_action", true), (Evaluate("Luz hizo pan", "lesson-luz-made").Verdict.Intent.Value, Evaluate("Luz hizo pan", "lesson-luz-made").Verdict.Communicated));
        Assert.Empty(Evaluate("sí, me gustó el pan de Luz", "lesson-liked").Verdict.Errors);
    }
}
