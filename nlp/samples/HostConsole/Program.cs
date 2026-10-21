// A host on the Vocaterra.NLP packages: loads a content bundle, builds the services with the scripted recognizer (no model files),
// evaluates the two demo utterances, then repeats the first one three times with the context carried by ConversationContext.Next. The calls are the ones in docs/API.md. Exit 0 when all checks pass.
using System.Globalization;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

const int ExitOk = 0;
const int ExitFailed = 1;

var bundleDir = args.Length > 0 ? args[0] : Path.Combine("content", "v1");

// 1. Load a content bundle: every file's hash and the cross-references are verified.
ContentBundle bundle;
try
{
    bundle = ContentLoader.Load(bundleDir);
}
catch (ContentValidationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitFailed;
}

Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"bundle {bundle.Manifest.BundleId} {bundle.Manifest.ContentVersion}, hash {bundle.ContentHash}"));

var offer = Evaluate(bundle, "yo tiene dos espada", "offer-items-open");
var price = Evaluate(bundle, "cuánto cuesta la espada", "ask-price-open");
Print("offer-items-open", "yo tiene dos espada", offer);
Print("ask-price-open", "cuánto cuesta la espada", price);

// The first utterance must give exactly the two demo errors; the second must score 1.
string[] expectedTags = ["conj.person", "noun.number"];
var offerTags = offer.Verdict.Errors.Select(static error => error.Tag).Order(StringComparer.Ordinal).ToArray();
var offerOk = offerTags.SequenceEqual(expectedTags, StringComparer.Ordinal);
var priceOk = price.Verdict.Correctness == 1.0;

Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"check offer-items-open: errors [{string.Join(", ", offerTags)}]: {(offerOk ? "PASS" : "FAIL")}"));
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"check ask-price-open: correctness {Format(price.Verdict.Correctness)}: {(priceOk ? "PASS" : "FAIL")}"));

// 3. Repeat the same mistake three times, carrying the context from one utterance to the next with ConversationContext.Next. The counts in
//    the context are the ones before each utterance, so the policy escalates from the first offence: recast, then hint, then hint.
var repeated = RepeatTheSameUtterance(bundle, "yo tiene dos espada", "offer-items-open", times: 3);
string[] expectedFeedback = ["recast", "hint", "hint"];
var repeatedOk = repeated.SequenceEqual(expectedFeedback, StringComparer.Ordinal);
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"check repeated offer: feedback [{string.Join(", ", repeated)}]: {(repeatedOk ? "PASS" : "FAIL")}"));
return offerOk && priceOk && repeatedOk ? ExitOk : ExitFailed;

// 2. Build the services, a pipeline and a conversation context, then evaluate one utterance. The scripted recognizer returns a fixed
//    transcript, so no model and no audio are needed; the stand-ins for the embedding model and the synthesizer produce no output of their own.
static UtterancePipeline PipelineFor(ContentBundle bundle, string transcript)
{
    IWordRecognizer words = new ScriptedWordRecognizer(
        new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, "scripted-transcript", RecognitionMode.Open));
    IPhonemeRecognizer? phonemes = null;
    IEmbeddingModel embeddings = new FakeEmbeddingModel();
    ISpeechSynthesizer synthesizer = new SilentSynthesizer();
    return new UtterancePipeline(new PipelineServices(words, phonemes, embeddings, synthesizer, bundle));
}

static ConversationContext FreshContext(ContentBundle bundle)
{
    return ConversationContext.Create(
        worldSeed: 42,
        agentId: "blacksmith",
        agentState: bundle.Agents["blacksmith"].InitialState(),
        now: new StoredTimestamp(1_760_000_000),
        learner: LearnerModel.New());
}

static PipelineResult Evaluate(ContentBundle bundle, string transcript, string promptId)
{
    return PipelineFor(bundle, transcript).Evaluate(PcmClip.FromSamples([]), bundle.Prompts[promptId], FreshContext(bundle));
}

// Evaluates the same utterance several times in a row and returns the feedback type of each evaluation's first item, in order.
static string[] RepeatTheSameUtterance(ContentBundle bundle, string transcript, string promptId, int times)
{
    var pipeline = PipelineFor(bundle, transcript);
    var prompt = bundle.Prompts[promptId];
    var context = FreshContext(bundle);
    var feedback = new string[times];
    for (var turn = 0; turn < times; turn++)
    {
        var result = pipeline.Evaluate(PcmClip.FromSamples([]), prompt, context);
        feedback[turn] = result.Decision.Feedback.Items[0].Type.ToString().ToLowerInvariant();
        context = context.Next(result, new StoredTimestamp(1_760_000_000 + turn + 1));
    }

    return feedback;
}

// 5. Read the verdict, the effects and the agent's lines.
static void Print(string promptId, string transcript, PipelineResult result)
{
    var verdict = result.Verdict;
    Console.WriteLine($"{promptId}: \"{transcript}\"");
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"  intent={verdict.Intent.Value} communicated={verdict.Communicated.ToString().ToLowerInvariant()} correctness={Format(verdict.Correctness)}"));
    foreach (var error in verdict.Errors)
    {
        Console.WriteLine($"  error {error.Tag}: expected '{error.Expected}', heard '{error.Actual}'");
    }

    foreach (var effect in result.Effects)
    {
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  effect {effect.EffectId} {effect.Kind} {Format(effect.Value)}"));
    }

    foreach (var line in result.Response.Lines)
    {
        Console.WriteLine($"  agent: {line.Text}");
    }
}

static string Format(double value)
{
    return value.ToString("0.###", CultureInfo.InvariantCulture);
}
