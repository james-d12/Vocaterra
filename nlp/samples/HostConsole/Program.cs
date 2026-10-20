// A host on the Vocaterra.NLP packages: loads a content bundle, builds the services with the scripted recognizer (no model files),
// evaluates the two demo utterances and checks their verdicts. The calls are the ones in docs/API.md. Exit 0 when both checks pass.
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
return offerOk && priceOk ? ExitOk : ExitFailed;

// 2. Build the services, a pipeline and a conversation context, then evaluate one utterance. The scripted recognizer returns a fixed
//    transcript, so no model and no audio are needed; the stand-ins for the embedding model and the synthesizer produce no output of their own.
static PipelineResult Evaluate(ContentBundle bundle, string transcript, string promptId)
{
    IWordRecognizer words = new ScriptedWordRecognizer(
        new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, "scripted-transcript", RecognitionMode.Open));
    IPhonemeRecognizer? phonemes = null;
    IEmbeddingModel embeddings = new FakeEmbeddingModel();
    ISpeechSynthesizer synthesizer = new SilentSynthesizer();
    var pipeline = new UtterancePipeline(new PipelineServices(words, phonemes, embeddings, synthesizer, bundle));

    var context = ConversationContext.Create(
        worldSeed: 42,
        agentId: "blacksmith",
        agentState: bundle.Agents["blacksmith"].InitialState(),
        now: new StoredTimestamp(1_760_000_000),
        learner: LearnerModel.New());

    PromptSpec prompt = bundle.Prompts[promptId];
    return pipeline.Evaluate(PcmClip.FromSamples([]), prompt, context);
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
