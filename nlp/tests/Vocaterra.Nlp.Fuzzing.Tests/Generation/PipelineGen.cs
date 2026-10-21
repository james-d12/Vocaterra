using System.Collections.Immutable;
using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>
/// Everything that decides one pipeline run: the transcript the scripted recognizer returns (and where it came from), the prompt, the
/// conversation context and the pipeline options. These are what a failure message prints.
/// </summary>
/// <param name="Transcript">What the scripted word recognizer returns.</param>
/// <param name="Source">Where the transcript came from: <c>correct</c>, <c>mutated:tag</c>, <c>answer</c> or <c>text</c>.</param>
/// <param name="PromptId">A prompt of the bundle.</param>
/// <param name="AgentId">The agent id of the context (the bundle's blacksmith, or one the bundle does not know).</param>
/// <param name="StateId">The agent's state.</param>
/// <param name="Counter">The agent's interaction counter.</param>
/// <param name="WorldSeed">The world seed.</param>
/// <param name="Phonemes">Whether a phoneme recognizer takes part (only for transcripts short enough to score).</param>
/// <param name="PronunciationMode">The pronunciation scorer.</param>
/// <param name="Concurrent">Whether the recognizers run on separate tasks.</param>
/// <param name="Embeddings">Whether the embedding rung of intent resolution is on.</param>
/// <param name="Learner">The learner model before the utterance.</param>
/// <param name="Repeats">The repeat counts per error tag.</param>
/// <param name="ExtraSkills">Host-named skills for the learner update (null or empty for none); may repeat the pipeline's own skill or an error family.</param>
/// <param name="HostSlots">Host facts for the response templates (null or empty for none); may share names with verdict slots.</param>
public sealed record PipelineCase(
    string Transcript,
    string Source,
    string PromptId,
    string AgentId,
    string StateId,
    long Counter,
    ulong WorldSeed,
    bool Phonemes,
    ScoringMode PronunciationMode,
    bool Concurrent,
    bool Embeddings,
    LearnerModel Learner,
    ImmutableSortedDictionary<string, int> Repeats,
    IReadOnlyList<string>? ExtraSkills = null,
    SlotMap? HostSlots = null)
{
    /// <summary>The longest transcript, in characters, that is paired with the phoneme recognizer (scoring grows with the cube of the phone count).</summary>
    public const int MaxScoredTranscriptCharacters = 150;

    /// <summary>The longest transcript the pipeline is fed; the adversarial corpus has strings of ten thousand characters.</summary>
    public const int MaxTranscriptCharacters = 2000;

    /// <inheritdoc />
    public override string ToString()
    {
        return CanonicalJson.Serialize(this);
    }
}

/// <summary>Generator of pipeline runs: transcripts from correct and mutated sentences of the prompt's own grammars, answers, free text and the adversarial corpus.</summary>
public static class PipelineGen
{
    // Host skills: ids that clash with the pipeline's own skill and with error families, and ids the pipeline never produces.
    private static readonly string[] HostSkillPool = ["word.árbol", "word.espada", "intent.offer_item", "intent.buy_item", "conj", "noun", "grammar.custom"];

    // Host slots whose values are valid for every formatter; the names include verdict slots (the verdict wins) and a reserved feedback name.
    private static readonly (string Name, string Value)[] HostSlotPool =
    [
        ("target", "árbol"), ("direction", "al norte"), ("distance", "3"), ("item", "martillo"), ("qty", "4"), ("tag", "x"), ("landmark", "puente"),
    ];

    private static readonly string[] SkillPool = ["conj", "noun", "gender", "vocab", "ser_estar", "word_order", "intent.buy_item", "intent.offer_item", "intent.ask_price"];

    /// <summary>Draws a run from a seed.</summary>
    public static Gen<PipelineCase> Case(PipelineHarness harness)
    {
        return Gen.ULong.Select(seed => Build(harness, new SeededRng(seed)));
    }

    /// <summary>Builds a run from a random stream.</summary>
    public static PipelineCase Build(PipelineHarness harness, SeededRng rng)
    {
        var prompts = harness.Bundle.Prompts.Values.ToArray();
        var prompt = rng.Pick(prompts);
        var (transcript, source) = Transcript(harness, rng, prompt);
        if (transcript.Length > PipelineCase.MaxTranscriptCharacters)
        {
            transcript = transcript[..PipelineCase.MaxTranscriptCharacters];
            source += ":truncated";
        }

        var blacksmith = harness.Bundle.Agents["blacksmith"].Definition;
        var known = rng.NextInt(5) != 0;
        var agentId = known ? blacksmith.AgentId : "nobody";
        var state = known ? rng.Pick(blacksmith.States.Select(static s => s.Id).ToArray()) : "idle";
        var skills = SkillPool.Where(_ => rng.NextInt(3) == 0)
            .Select(skill => new SkillMastery(skill, Math.Round(rng.NextDouble(), 6), 1 + rng.NextInt(9), rng.NextInt(5)))
            .ToArray();
        var learner = new LearnerModel(BktParameters.Default, skills);
        return new PipelineCase(
            transcript,
            source,
            prompt.PromptId,
            agentId,
            state,
            rng.NextInt(4) == 0 ? rng.NextInt(100_000) : rng.NextInt(6),
            rng.NextUInt64(),
            rng.NextInt(2) == 0 && transcript.Length <= PipelineCase.MaxScoredTranscriptCharacters,
            rng.NextInt(2) == 0 ? ScoringMode.Gop : ScoringMode.Greedy,
            rng.NextInt(2) == 0,
            rng.NextInt(4) != 0,
            learner,
            DecisionGen.RandomRepeats(rng),
            rng.NextInt(3) == 0 ? [.. HostSkillPool.Where(_ => rng.NextInt(3) == 0)] : null,
            rng.NextInt(3) == 0 ? SlotMap.From(HostSlotPool.Where(_ => rng.NextInt(2) == 0).Select(static p => new KeyValuePair<string, string>(p.Name, p.Value))) : null);
    }

    private static (string Transcript, string Source) Transcript(PipelineHarness harness, SeededRng rng, PromptSpec prompt)
    {
        switch (rng.NextInt(10))
        {
            case < 3:
                var correct = CorrectSentence(harness, rng, prompt);
                if (correct is not null)
                {
                    return (correct, "correct");
                }

                break;
            case < 6:
                var mutated = MutationGen.Build(harness, new MutationCase(rng.Pick(Enum.GetValues<MutationKind>()), rng.NextUInt64()));
                if (mutated is not null)
                {
                    return (mutated.Mutated.Text, "mutated:" + mutated.Tag);
                }

                break;
            case < 8:
                var set = harness.Bundle.AnswerSets[prompt.AnswerSetId];
                var answer = rng.Pick(set.Answers.ToArray());
                var phrase = rng.Pick(new[] { answer.Canonical }.Concat(answer.Aliases).ToArray());
                var noise = new TextNoise(rng.NextInt(3) == 0 ? rng.NextInt(5) : 0, 0, false, rng.NextInt(3) == 0 ? rng.NextInt(5) : 0, 0, 0, -1, rng.NextInt(1 << 20));
                return (noise.Apply(phrase), "answer");
            default:
                break;
        }

        return (SampleText(harness, rng), "text");
    }

    // A correct sentence of one of the grammars the prompt's intents (or its own grammar ids) allow.
    private static string? CorrectSentence(PipelineHarness harness, SeededRng rng, PromptSpec prompt)
    {
        var grammars = harness.Grammars.Grammars
            .Where(g => prompt.SlotGrammarIds.Contains(g.Id, StringComparer.Ordinal) || g.IntentIds.Any(intent => prompt.AllowedIntentIds.Contains(intent, StringComparer.Ordinal)))
            .ToArray();
        if (grammars.Length == 0)
        {
            return null;
        }

        var grammar = rng.Pick(grammars);
        var sentence = SentenceGen.Realize(harness, new SentenceRecipe("catalog", grammar.Pattern, rng.NextUInt64()));
        return sentence?.Text;
    }

    // Free text drawn by a one-off CsCheck-free path: a lexicon phrase, noised, or an adversarial string.
    private static string SampleText(PipelineHarness harness, SeededRng rng)
    {
        if (rng.NextInt(4) == 0)
        {
            return rng.Pick(AdversarialCorpus.Strings.ToArray());
        }

        var words = Enumerable.Range(0, 1 + rng.NextInt(8)).Select(_ => rng.Pick(harness.Words.AllForms)).ToArray();
        var noise = new TextNoise(rng.NextInt(5) == 0 ? rng.NextInt(5) : 0, rng.NextInt(5) == 0 ? rng.NextInt(4) : 0, rng.NextInt(6) == 0, rng.NextInt(4) == 0 ? rng.NextInt(5) : 0, rng.NextInt(5) == 0 ? rng.NextInt(4) : 0, rng.NextInt(6) == 0 ? rng.NextInt(3) : 0, -1, rng.NextInt(1 << 20));
        return noise.Apply(string.Join(' ', words));
    }
}
