using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Tests for the host inputs of an utterance: <see cref="ConversationContext.HostSlots"/> and <see cref="ConversationContext.ExtraSkills"/>.</summary>
public sealed class HostInputsTests
{
    // Two variants of the villager's give reply (the pool has two) and of the conjugation recast, each reading host slots through the same formatters verdict
    // slots use.
    private static readonly string[] OfferReplies =
    [
        "Mira {target:article(def)}, {direction}, a {distance:words(m)} metro.",
        "Fíjate en {target:article(def)}, {direction}, a {distance:words(m)} metro.",
    ];

    private readonly ContentBundle _bundle;

    public HostInputsTests()
    {
        _bundle = FixtureBundle.Create(
            pools =>
            {
                TwoPhaseTests.SetVariants(pools, "villager_give", OfferReplies);
                TwoPhaseTests.SetVariants(
                    pools,
                    "feedback.recast.conj.person",
                    "Ah, {verb:conj(2,sg,pres)} {qty:words(item)} {item:plural-if(qty)} cerca de {landmark:article(def)}.",
                    "Ah, ¿{verb:conj(2,sg,pres)} {qty:words(item)} {item:plural-if(qty)}? Junto a {landmark:article(def)}.");
            });
    }

    private static SlotMap Slots(params (string Key, string Value)[] pairs)
    {
        return SlotMap.From(pairs.Select(static pair => new KeyValuePair<string, string>(pair.Key, pair.Value)));
    }

    private static SlotMap FullHost()
    {
        return Slots(("target", "árbol"), ("direction", "a la derecha"), ("distance", "1"), ("landmark", "puente"), ("verb", "querer"));
    }

    private static ConversationContext Context(SlotMap? hostSlots = null, IReadOnlyList<string>? extraSkills = null)
    {
        return ConversationContext.Create(
            42,
            "villager",
            new AgentState("villager", "idle", 0),
            new StoredTimestamp(1_700_000_000),
            LearnerModel.New(),
            null,
            hostSlots,
            extraSkills);
    }

    private UtterancePipeline Pipeline(string transcript = TestSupport.WorkedExample)
    {
        var services = new PipelineServices(new ScriptedWordRecognizer(TestSupport.Asr(transcript)), null, null, null, _bundle);
        return new UtterancePipeline(services);
    }

    // The edited reply pool needs host slots, so a context without any gets the full set (the hash is not read in those tests).
    private PipelineResult Run(ConversationContext context, string transcript = TestSupport.WorkedExample)
    {
        var supplied = context.HostSlots is null ? context with { HostSlots = FullHost() } : context;
        return Pipeline(transcript).Evaluate(TestSupport.Clip(), _bundle.Prompts[TestSupport.OpenPrompt], supplied);
    }

    [Fact]
    public void AReplyTemplateRendersAHostSlotWithArticleDefAndWords()
    {
        var result = Run(Context(FullHost()));

        var reply = result.Response.Lines.Single(static line => line.LineId.StartsWith("villager_give", StringComparison.Ordinal));
        Assert.Contains("el árbol, a la derecha, a un metro.", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AFeedbackTemplateReadsHostSlotsToo()
    {
        var result = Run(Context(FullHost()));

        var recast = result.Response.Lines.Single(static line => line.IsRecast);
        Assert.Contains("el puente", recast.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AVerdictSlotWinsOverAHostSlotOfTheSameName()
    {
        var plain = Run(Context(FullHost()));
        var withHostItem = Run(Context(FullHost().With("item", "martillo").With("qty", "7")));

        Assert.Equal("espada", withHostItem.Verdict.Slots["item"]);
        Assert.Equal(plain.Response, withHostItem.Response);
    }

    [Fact]
    public void ReservedFeedbackNamesWinOverHostSlots()
    {
        var planner = new ResponsePlanner(_bundle.TemplateEngine);
        var catalog = new DialogueCatalog(
        [
            new("greet", new VariantPool("greet", [new DialogueTemplate("g.1", "Hola, {who}.")])),
            new("feedback.explicit_correction", new VariantPool("feedback.explicit_correction", [new DialogueTemplate("f.1", "Se dice {expected}, no {actual} ({tag}, {who}).")])),
        ]);
        var verdict = Run(Context()).Verdict with { Intent = new IntentId("greet"), Slots = SlotMap.Empty };

        var plan = planner.Plan(
            verdict,
            new FeedbackRequest("conj.person", "tengo", "tiene", FeedbackKinds.ExplicitCorrection),
            catalog,
            SeededRng.ForInteraction(1, "a", 0),
            hostSlots: Slots(("who", "viajero"), ("expected", "otra cosa"), ("tag", "x")));

        Assert.Equal("Hola, viajero.", plan.Lines[0].Text);
        Assert.Equal("Se dice tengo, no tiene (conj.person, viajero).", plan.Lines[1].Text);
    }

    [Fact]
    public void AFeedbackTemplateNamingAMissingHostSlotFallsBackToTheGenericPoolLikeAMissingVerdictSlot()
    {
        // Without a "landmark" the specific recast pool cannot render; the generic feedback.recast pool answers and the reply is untouched.
        var withoutLandmark = Run(Context(Slots(("target", "árbol"), ("direction", "al norte"), ("distance", "1"), ("verb", "querer"))));

        var recast = withoutLandmark.Response.Lines.Single(static line => line.IsRecast);
        Assert.StartsWith("feedback.recast.", recast.LineId, StringComparison.Ordinal);
        Assert.DoesNotContain("conj.person", recast.LineId, StringComparison.Ordinal);
        Assert.Contains(withoutLandmark.Response.Lines, static line => line.LineId.StartsWith("villager_give", StringComparison.Ordinal));
    }

    [Fact]
    public void AReplyTemplateNamingAMissingHostSlotThrowsTheTemplateException()
    {
        // No host slots at all: the same failure a missing verdict slot gives today.
        var ex = Assert.Throws<TemplateException>(() =>
            Pipeline().Evaluate(TestSupport.Clip(), _bundle.Prompts[TestSupport.OpenPrompt], Context()));

        Assert.Contains("target", ex.Placeholder, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEnvelopeCarriesHostSlotsAndSkills_AndReplayReproducesTheResult()
    {
        var pipeline = Pipeline();
        var context = Context(FullHost(), ["word.árbol", "word.puente"]);
        var trace = pipeline.Trace(TestSupport.Clip(), _bundle.Prompts[TestSupport.OpenPrompt], context);
        var envelope = ReplayEnvelope.From(trace, context);

        using var writer = new StringWriter();
        new ReplayLog(writer).Append(envelope);
        var text = writer.ToString();
        var read = ReplayReader.ReadEnvelopes(new StringReader(text)).Single();

        Assert.Contains("\"hostSlots\":{\"direction\":\"a la derecha\"", text, StringComparison.Ordinal);
        Assert.Contains("\"extraSkills\":[\"word.puente\",\"word.árbol\"]", text, StringComparison.Ordinal);
        Assert.Equal(context.HostSlots, read.Context.HostSlots);
        Assert.Equal(context.ExtraSkills, read.Context.ExtraSkills);
        Assert.Equal(context, read.Context);
        Assert.Null(Replayer.Replay(read, pipeline, static hash => new Vocaterra.Nlp.Core.Audio.PcmClip([], Vocaterra.Nlp.Core.Audio.PcmClip.DefaultSampleRate, hash)));
        Assert.NotNull(trace.Result.Decision.Learner.Find("word.árbol"));
    }

    [Fact]
    public void ReplayShowsADifferenceWhenTheRecordedHostSlotsAreRemoved()
    {
        var pipeline = Pipeline();
        var context = Context(FullHost());
        var envelope = ReplayEnvelope.From(pipeline.Trace(TestSupport.Clip(), _bundle.Prompts[TestSupport.OpenPrompt], context), context);
        var changed = envelope with { Context = envelope.Context with { HostSlots = Slots(("target", "pozo"), ("direction", "a la derecha"), ("distance", "1"), ("landmark", "puente"), ("verb", "querer")) } };

        var diff = Replayer.Replay(changed, pipeline, static hash => new Vocaterra.Nlp.Core.Audio.PcmClip([], Vocaterra.Nlp.Core.Audio.PcmClip.DefaultSampleRate, hash));

        Assert.NotNull(diff);
        Assert.Contains(diff.Paths, static path => path.StartsWith("/response", StringComparison.Ordinal));
    }

    [Fact]
    public void ContextsDifferingOnlyInHostSlotsOrSkillsHaveDifferentHostStateHashes()
    {
        var plain = Context();
        var withSlots = Context(Slots(("target", "árbol")));
        var otherSlots = Context(Slots(("target", "pozo")));
        var withSkills = Context(extraSkills: ["word.árbol"]);
        var both = Context(Slots(("target", "árbol")), ["word.árbol"]);

        var hashes = new[] { plain, withSlots, otherSlots, withSkills, both }.Select(static c => c.HostStateHash).ToArray();

        Assert.Equal(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EmptyHostSlotsAndSkillsHashExactlyAsBefore()
    {
        var plain = Context();
        var explicitEmpty = Context(SlotMap.Empty, []);
        var learner = LearnerModel.New();
        var legacy = CanonicalJson.Hash(new LegacySnapshot(42, "villager", new AgentState("villager", "idle", 0), new StoredTimestamp(1_700_000_000), learner, new SortedDictionary<string, int>(StringComparer.Ordinal)));

        Assert.Equal(legacy, plain.HostStateHash);
        Assert.Equal(legacy, explicitEmpty.HostStateHash);
        Assert.Null(explicitEmpty.HostSlots);
        Assert.Null(explicitEmpty.ExtraSkills);
        Assert.Equal(plain, explicitEmpty);
        Assert.DoesNotContain("hostSlots", CanonicalJson.Serialize(plain), StringComparison.Ordinal);
        Assert.DoesNotContain("extraSkills", CanonicalJson.Serialize(plain), StringComparison.Ordinal);
    }

    [Fact]
    public void TheHashDoesNotDependOnSlotInsertionOrderOrSkillOrderAndDuplicates()
    {
        var a = Context(Slots(("a", "1"), ("b", "2")), ["word.b", "word.a", "word.b"]);
        var b = Context(Slots(("b", "2"), ("a", "1")), ["word.a", "word.b"]);

        Assert.Equal(a.HostStateHash, b.HostStateHash);
        Assert.Equal(["word.a", "word.b"], a.ExtraSkills);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NextDoesNotCarryHostSlotsOrSkillsOver()
    {
        var context = Context(FullHost(), ["word.árbol"]);
        var result = Run(context);

        var next = context.Next(result, new StoredTimestamp(1_700_000_100));

        Assert.Null(next.HostSlots);
        Assert.Null(next.ExtraSkills);
        Assert.Equal(next.HostStateHash, ConversationContext.Create(next.WorldSeed, next.AgentId, next.AgentState, next.Now, next.Learner, next.RepeatCountsByTag).HostStateHash);
        Assert.NotNull(next.Learner.Find("word.árbol"));
    }

    [Fact]
    public void ExtraSkillsAreSteppedOnce_AndErrorFamiliesAreNotCounted_Twice()
    {
        var without = Run(Context()).Decision.Learner;
        var with = Run(Context(extraSkills: ["word.árbol", "word.árbol"])).Decision.Learner;

        var added = with.Skills.Where(skill => without.Find(skill.SkillId) is null).Select(static skill => skill.SkillId).ToArray();
        Assert.Equal(["word.árbol"], added);
        Assert.Equal(1, with.Find("word.árbol")!.Attempts);
        Assert.Equal(0, with.Find("word.árbol")!.Errors);
        foreach (var skill in without.Skills)
        {
            Assert.Equal(skill, with.Find(skill.SkillId));
        }
    }

    [Fact]
    public void AHostSkillEqualToThePipelinesOwnSkillOrAnErrorFamilyStepsOnce()
    {
        var without = Run(Context()).Decision.Learner;
        var own = without.Skills.Single(static skill => skill.SkillId.StartsWith("intent.", StringComparison.Ordinal)).SkillId;
        var family = without.Skills.First(static skill => !skill.SkillId.StartsWith("intent.", StringComparison.Ordinal)).SkillId;

        var with = Run(Context(extraSkills: [own, family])).Decision.Learner;

        Assert.Equal(without, with);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("word. árbol")]
    [InlineData("word.árbol\t")]
    public void InvalidSkillIdsAreRejected(string skill)
    {
        Assert.Throws<ArgumentException>(() => Context(extraSkills: [skill]));
        Assert.Throws<ArgumentException>(() => Context() with { ExtraSkills = [skill] });
    }

    [Fact]
    public void ANullSkillIdIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Context(extraSkills: [null!]));
    }

    private sealed record LegacySnapshot(
        ulong WorldSeed,
        string AgentId,
        AgentState AgentState,
        StoredTimestamp Now,
        LearnerModel Learner,
        IReadOnlyDictionary<string, int> RepeatCountsByTag);
}
