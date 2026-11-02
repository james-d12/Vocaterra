using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Es.Normalization;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Host.Tests;

/// <summary>
/// The village's talk on the host's path (<c>--transcript … --prompt …</c>, VILLAGE_PLAN V14): a statement at the smith's open prompt, <c>tener que</c> against
/// <c>tener</c> with a noun, and the listener's name said as a vocative, which the host reads as the game does (<see cref="Vocative"/>); and since V17 a plan at the
/// baker's open prompt, and the simple future with the later markers at the steps of Mañana en el pueblo.
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
    [InlineData("baker-open", "mañana voy a comprar pan")] // the orchestrator's case at M57: unknown and vocab.wrong before V17 (the prompt took no state_action)
    [InlineData("baker-open", "mañana compraré pan")]
    [InlineData("baker-open", "la semana que viene compraré pan")]
    public void APlanAtTheBakersOpenPromptIsAStatementAndErrorFree(string prompt, string line)
    {
        // V17: the baker's open prompt takes state_action (her agent answered it already), and her answer set holds the plans, so the core finds no wrong word. The
        // fisher's does not: his ask_directions takes buscar with any noun (busco la panadería), so buscaré madera would be his directions (the lint's confusion check).
        var verdict = Evaluate(line, prompt).Verdict;

        Assert.Equal(("state_action", true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Empty(verdict.Errors);
    }

    [Theory]
    [InlineData("mañana compraré pan", "lesson-will-buy", "pan")] // the gate's demos
    [InlineData("la semana que viene iré al río", "lesson-will-go", "río")] // a later marker, one phrase of the grammar since V17: before it, semana was the item
    [InlineData("pasado mañana iré al río", "lesson-will-go", "río")]
    [InlineData("la semana que viene compraré pan", "lesson-will-buy", "pan")]
    public void TheSimpleFutureWithAMarkerIsAStatementAndErrorFree(string line, string prompt, string slot)
    {
        var verdict = Evaluate(line, prompt).Verdict;

        Assert.Equal(("state_action", true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Empty(verdict.Errors);
        Assert.Contains(slot, verdict.Slots.Values);
    }

    [Theory]
    [InlineData("la semana que viene compré pan", "voy a comprar")]
    [InlineData("mañana compré pan", "voy a comprar")]
    [InlineData("ayer compraré pan", "compré")]
    public void APastWithAFutureMarkerOrAFutureWithAPastOneIsTheCoresTenseError(string line, string expected)
    {
        var verdict = Evaluate(line, "lesson-will-buy").Verdict;

        Assert.Equal(("state_action", true, "pan"), (verdict.Intent.Value, verdict.Communicated, verdict.Slots["item"]));
        Assert.Equal(expected, Assert.Single(verdict.Errors, static e => e.Tag == "conj.tense").Expected);
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
    [InlineData("baker-open", "Luz, mañana compraré pan", "state_action", "pan")] // V17: a plan at the baker's open prompt, the gate's demo
    [InlineData("lesson-will-fetch", "Domingo, buscaré madera para Luz", "state_action", "madera")] // the smith's name goes, Luz's stays: para joins it to the line
    [InlineData("lesson-luz-will", "Mar, Luz hará pan", "state_action", "pan")]
    [InlineData("baker-open", "buenos días luz quiero pan", "request_item", "pan")] // V19, the review's R4: "No te entendí" before (the name after hola alone)
    [InlineData("smith-open", "buenos días domingo vendo madera", "offer_item", "madera")] // a statement about Sunday before, recast «domingo vendo madera»
    [InlineData("fisher-open", "buenas tardes mar ¿dónde está la panadería?", "ask_directions", "panadería")]
    [InlineData("farmer-open", "buen día león quiero tres manzanas", "request_item", "manzana")] // vocab.wrong on león before
    [InlineData("baker-open", "hola luz quiero pan", "request_item", "pan")]
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

    [Theory]
    [InlineData("baker-open", "vendo madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")] // V15: the gate's demos, a sale at the baker's open prompt
    [InlineData("baker-open", "quiero vender madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("lesson-sell", "quiero vender madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("smith-open", "tengo madera para usted", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("farmer-open", "¿cuánto es todo?", "ask_total", "Quince manzanas, quince monedas.")]
    [InlineData("farmer-open", "¿puedo pagar con fruta?", "ask_pay", "Aquí se paga con monedas. Pero yo te compro fruta.")]
    [InlineData("baker-open", "hola, quiero dos panes", "request_item", "Toma tu pan. ¡Gracias!")] // a greeting before a request: the rest is answered (V15)
    [InlineData("baker-open", "hola Luz quiero dos panes", "request_item", "Toma tu pan. ¡Gracias!")]
    [InlineData("baker-open", "buenas tardes, ¿cuánto cuesta el pan?", "ask_price", "El pan cuesta dos monedas.")] // V15b: no vocab.wrong on the greeting
    [InlineData("baker-open", "buen día, ¿cuánto cuesta el pan?", "ask_price", "El pan cuesta dos monedas.")]
    [InlineData("farmer-open", "buenos días, ¿puedo pagar con fruta?", "ask_pay", "Aquí se paga con monedas. Pero yo te compro fruta.")]
    [InlineData("baker-open", "vendo tres bloques de madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")] // V15b: wood counted in blocks
    [InlineData("baker-open", "te vendo tres bloques de madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("baker-open", "quiero vender tres bloques de madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("lesson-sell", "quiero vender tres bloques de madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("smith-open", "vendo dos bloques de madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("farmer-open", "vendo dos frutas", "offer_item", "¡Gracias! Aquí tienes tus monedas.")] // V19: the answer to León's «¿Cuántas frutas me vendes?» (vocab.wrong on vendo before)
    [InlineData("farmer-open", "te vendo tres frutas", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("farmer-open", "vendo una fruta", "offer_item", "¡Gracias! Aquí tienes tus monedas.")]
    [InlineData("baker-open", "te vendo cuatro bloques de madera", "offer_item", "¡Gracias! Aquí tienes tus monedas.")] // the answer to «¿Cuántos bloques de madera me vendes?»
    [InlineData("farmer-open", "quiero quince manzanas", "request_item", "Aquí tienes. ¡Gracias!")] // V19: no cap (the host has no bag; the game decides)
    [InlineData("baker-open", "quiero cero panes", "request_item", "Aquí tienes el pan. ¡Gracias!")] // the host has no bag: the game answers none_asked
    public void TheMarketsLinesAreTheirIntentsCommunicatedAndErrorFree(string prompt, string line, string intent, string reply)
    {
        var result = Evaluate(line, prompt);

        Assert.Equal((intent, true), (result.Verdict.Intent.Value, result.Verdict.Communicated));
        Assert.Empty(result.Verdict.Errors);
        Assert.Contains(result.Response.Lines, l => l.Text == reply || (reply.EndsWith("¡Gracias!", StringComparison.Ordinal) && l.Text.EndsWith("¡Gracias!", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("baker-open", "¿cuánto costa el pan?", "ask_price", "conj.irregular", "cuesta")] // M58: the stand-in examples restored at V15
    [InlineData("baker-open", "hay pans", "ask_availability", "noun.number", "panes")]
    [InlineData("lesson-dear", "la manzana es muy caro", "describe", "gender.agree", "cara")] // M62
    [InlineData("lesson-cheaper", "la manzana es más barato que el pan", "describe", "gender.agree", "barata")]
    [InlineData("farmer-open", "¿cuánto costa todo?", "ask_total", "conj.irregular", "cuesta")]
    [InlineData("farmer-open", "¿podo pagar con fruta?", "ask_pay", "conj.irregular", "puedo")]
    [InlineData("lesson-will-sell", "mañana vendí fruta", "state_action", "conj.tense", "voy a vender")]
    [InlineData("lesson-will-sell", "yo venderás fruta", "state_action", "conj.person", "venderé")]
    [InlineData("baker-open", "buenos días, ¿cuánto cuestan el pan?", "ask_price", "conj.person", "cuesta")] // V15b: the error after a greeting stays, alone
    public void TheMarketsSlipsAreGradedAndTheIntentHolds(string prompt, string line, string intent, string tag, string expected)
    {
        var verdict = Evaluate(line, prompt).Verdict;

        Assert.Equal((intent, true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Equal(expected, Assert.Single(verdict.Errors, e => e.Tag == tag).Expected);
        Assert.DoesNotContain(verdict.Errors, static e => e.Tag == "vocab.wrong");
    }

    [Theory]
    [InlineData("fuimos al río")] // the canonical answer
    [InlineData("mar y yo fuimos al río")] // V19, the review's R1 (the game's side): vocab.wrong on mar, correctness 0.4, before the answer set had a subject
    [InlineData("ayer mar y yo fuimos al río")]
    [InlineData("mar y yo fuimos al río ayer")]
    [InlineData("tú y yo fuimos al río")]
    [InlineData("ayer tú y yo fuimos al río")]
    [InlineData("tú y yo fuimos al río ayer")]
    [InlineData("luz y yo fuimos al río")]
    [InlineData("ayer luz y yo fuimos al río")]
    [InlineData("luz y yo fuimos al río ayer")]
    public void SessionEightsAnswerWithASubjectScoresAsTheCanonicalOne(string line)
    {
        // «¿Y Mar y tú? ¿Adónde fueron ayer?» invites the subject; the core grades a coordinated subject right since M64, and the answer set holds the answers with one.
        var verdict = Evaluate(line, "lesson-we-went").Verdict;

        Assert.Equal(("state_action", true, 1.0), (verdict.Intent.Value, verdict.Communicated, verdict.Correctness));
        Assert.Empty(verdict.Errors);
    }

    [Theory]
    [InlineData("baker-open", "vendí madera")] // the DSL has no tense: offer_item names the present, state_action takes vender's other forms
    [InlineData("baker-open", "venderé madera")]
    [InlineData("farmer-open", "venderé fruta")] // the farmer's open prompt takes state_action since V15, so a plan is no sale
    public void APastOrAFutureOfVenderIsAStatementNotAnOffer(string prompt, string line) =>
        Assert.Equal("state_action", Evaluate(line, prompt).Verdict.Intent.Value);
}
