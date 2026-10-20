using System.Collections.Immutable;
using CsCheck;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Taxonomy;
using Slg.Decision.Feedback;
using Slg.Decision.Learning;
using Slg.Decision.Npc;
using Slg.Decision.Scheduling;
using Slg.Fuzzing.Tests.Oracles;

namespace Slg.Fuzzing.Tests.Generation;

/// <summary>One scripted review of a card: the rating and the seconds that passed since the previous review (negative means the clock went back).</summary>
/// <param name="Rating">The FSRS grade.</param>
/// <param name="DeltaSeconds">Seconds since the previous review.</param>
public sealed record ReviewStep(Rating Rating, long DeltaSeconds);

/// <summary>A card history: when it starts, which parameters schedule it, and the reviews applied in order.</summary>
/// <param name="Start">The first review time, in Unix seconds.</param>
/// <param name="DesiredRetention">Desired retention in (0, 1).</param>
/// <param name="MaximumIntervalDays">Longest interval in days.</param>
/// <param name="Steps">The reviews.</param>
public sealed record ReviewScript(long Start, double DesiredRetention, int MaximumIntervalDays, IReadOnlyList<ReviewStep> Steps)
{
    /// <summary>The scheduler parameters of the script.</summary>
    public FsrsParameters Parameters => new(FsrsParameters.Default.Weights, DesiredRetention, MaximumIntervalDays);
}

/// <summary>
/// Generators for the decision layer: NPC state machines (the bundle's, random valid ones, and valid ones with one structural defect), BKT parameters,
/// spaced-repetition review scripts and feedback policies. Everything is built from a <see cref="SeededRng"/> seed, so a CsCheck seed replays it.
/// </summary>
public static class DecisionGen
{
    private static readonly string[] Intents = ["offer_item", "buy_item", "ask_price", "cast_spell", "greet", "unknown", "zzz"];
    private static readonly string[] Dialogue = ["greet_reply", "offer_item_reply", "not_understood", "trade_done", "idle_chat", "zzz"];
    private static readonly string[] Events = ["trade.offer", "trade.done", "quest.start", "shop.open"];
    private static readonly string[] Patterns = ["*", "conj.*", "gender.*", "noun.*", "pron.*", "vocab.*", "ser_estar", "word_order", "conj.person", "pron.r", "vocab.unknown", "zzz.*", "zzz"];

    /// <summary>Verdict-independent NPC cases: the machine, a state it declares, a counter and a world seed.</summary>
    public static Gen<NpcCase> Npc(PipelineHarness harness)
    {
        return Gen.ULong.Select(seed => BuildNpc(harness, new SeededRng(seed)));
    }

    /// <summary>Builds an NPC case from a random stream: the bundle's NPCs one time in three, otherwise a random valid definition.</summary>
    public static NpcCase BuildNpc(PipelineHarness harness, SeededRng rng)
    {
        var definition = rng.NextInt(3) == 0
            ? rng.Pick(harness.Bundle.Npcs.Values.Select(static m => m.Definition).ToArray())
            : RandomDefinition(rng);
        var stateIds = definition.States.Select(static s => s.Id).ToArray();
        var counter = rng.NextInt(5) == 0 ? (long)rng.NextInt(1_000_000) * 1000 : rng.NextInt(20);
        return new NpcCase(definition, rng.Pick(stateIds), counter, rng.NextUInt64(), rng.NextUInt64());
    }

    /// <summary>A valid random NPC definition: up to five states, up to three variant pools, conditions over intents, error-tag patterns and the communicated flag.</summary>
    public static NpcDefinition RandomDefinition(SeededRng rng)
    {
        var stateCount = 1 + rng.NextInt(5);
        var stateIds = Enumerable.Range(0, stateCount).Select(static i => "s" + i).ToArray();
        var pools = Enumerable.Range(0, rng.NextInt(4)).Select(i => new VariantPoolDefinition("pool" + i, 1 + rng.NextInt(5))).ToArray();

        NpcActionDefinition Action()
        {
            var pool = pools.Length > 0 && rng.NextInt(2) == 0 ? rng.Pick(pools).Id : null;
            return new NpcActionDefinition(rng.Pick(Dialogue), rng.NextInt(3) == 0 ? rng.Pick(Events) : null, pool);
        }

        IReadOnlyList<NpcActionDefinition> Actions() => [.. Enumerable.Range(0, rng.NextInt(4)).Select(_ => Action())];

        NpcCondition Condition()
        {
            return new NpcCondition(
                rng.NextInt(2) == 0 ? rng.Pick(Intents) : null,
                rng.NextInt(3) == 0 ? [.. Enumerable.Range(0, 1 + rng.NextInt(3)).Select(_ => rng.Pick(Patterns))] : null,
                rng.NextInt(4) == 0 ? [.. Enumerable.Range(0, 1 + rng.NextInt(2)).Select(_ => rng.Pick(Patterns))] : null,
                rng.NextInt(3) == 0 ? rng.NextInt(2) == 0 : null);
        }

        var states = stateIds.Select(id => new NpcStateDefinition(
            id,
            [.. Enumerable.Range(0, rng.NextInt(5)).Select(_ => new NpcTransition(Condition(), rng.Pick(stateIds), Actions()))],
            rng.NextInt(3) == 0 ? null : new NpcFallback(Actions(), rng.NextInt(2) == 0 ? null : rng.Pick(stateIds)))).ToArray();
        return new NpcDefinition("npc" + rng.NextInt(100), rng.Pick(stateIds), states, pools.Length == 0 && rng.NextInt(2) == 0 ? null : pools);
    }

    /// <summary>A valid definition with exactly one structural defect that <c>NpcStateMachine</c> must reject with an <see cref="InvalidDataException"/>.</summary>
    public static Gen<BrokenNpc> BrokenNpc()
    {
        return Gen.ULong.Select(seed =>
        {
            var rng = new SeededRng(seed);
            var definition = RandomDefinition(rng);

            // Make sure there is something to break: a transition with an action, a variant pool.
            definition = definition with
            {
                States =
                [
                    definition.States[0] with
                    {
                        Transitions =
                        [
                            new NpcTransition(new NpcCondition(), definition.States[0].Id, [new NpcActionDefinition("greet_reply", null, null)]),
                            .. definition.States[0].Transitions,
                        ],
                    },
                    .. definition.States.Skip(1),
                ],
                VariantPools = [new VariantPoolDefinition("poolx", 2), .. definition.VariantPools ?? []],
            };

            var kind = rng.NextInt(10);
            var first = definition.States[0];
            var broken = kind switch
            {
                0 => (definition with { NpcId = string.Empty }, "empty npcId"),
                1 => (definition with { States = [] }, "no states"),
                2 => (definition with { InitialState = "nowhere" }, "undefined initial state"),
                3 => (definition with { States = [.. definition.States, first] }, "duplicate state id"),
                4 => (definition with { States = [first with { Transitions = [new NpcTransition(new NpcCondition(), "nowhere", []), .. first.Transitions] }, .. definition.States.Skip(1)] }, "transition to an undefined state"),
                5 => (definition with { VariantPools = [new VariantPoolDefinition("poolx", 0), .. definition.VariantPools ?? []] }, "variant pool of size 0"),
                6 => (definition with { VariantPools = [new VariantPoolDefinition("poolx", 2), new VariantPoolDefinition("poolx", 3), .. definition.VariantPools ?? []] }, "duplicate variant pool"),
                7 => (definition with { States = [first with { Transitions = [new NpcTransition(new NpcCondition(), first.Id, [new NpcActionDefinition("greet_reply", null, "missing-pool")]), .. first.Transitions] }, .. definition.States.Skip(1)] }, "undefined variant pool"),
                8 => (definition with { States = [first with { Transitions = [new NpcTransition(new NpcCondition(AnyErrorTag: ["a*b"]), first.Id, []), .. first.Transitions] }, .. definition.States.Skip(1)] }, "malformed error-tag pattern"),
                _ => (definition with { States = [first with { Transitions = [new NpcTransition(new NpcCondition(NoErrorTag: []), first.Id, []), .. first.Transitions] }, .. definition.States.Skip(1)] }, "empty noErrorTag list"),
            };
            return new BrokenNpc(broken.Item1, broken.Item2);
        });
    }

    /// <summary>A probability: uniform, the edges 0 and 1, or a hair inside an edge.</summary>
    public static Gen<double> Probability { get; } = Gen.Frequency(
        (6, (IGen<double>)Gen.Double[0.0, 1.0]),
        (2, (IGen<double>)Gen.OneOfConst(0.0, 1.0, 0.5)),
        (1, (IGen<double>)Gen.OneOfConst(1e-12, 1.0 - 1e-12, double.Epsilon, Math.BitDecrement(1.0))));

    /// <summary>Valid BKT parameters (every value in [0, 1], edges included) and, separately, whether they satisfy the usual slip + guess &lt;= 1.</summary>
    public static Gen<BktParameters> Bkt { get; } = Gen.Select(
        Probability,
        Probability,
        Probability,
        Probability,
        static (prior, learn, slip, guess) => new BktParameters(prior, learn, slip, guess));

    /// <summary>A review script of one to thirty reviews.</summary>
    public static Gen<ReviewScript> Script { get; } = Gen.ULong.Select(static seed =>
    {
        var rng = new SeededRng(seed);
        var steps = Enumerable.Range(0, 1 + rng.NextInt(30)).Select(_ =>
        {
            var delta = rng.NextInt(10) switch
            {
                0 => 0L,
                1 => -(long)rng.NextInt(86_400 * 3),
                2 => (long)rng.NextInt(3600),
                3 => 86_400L * (1 + rng.NextInt(400)),
                _ => 86_400L * rng.NextInt(30),
            };
            return new ReviewStep((Rating)(1 + rng.NextInt(4)), delta);
        }).ToArray();
        var retention = rng.NextInt(3) == 0 ? Math.Round(0.7 + (rng.NextDouble() * 0.28), 3) : 0.9;
        var maximum = rng.NextInt(4) == 0 ? 30 + rng.NextInt(400) : 36_500;
        return new ReviewScript(1_000_000_000L + rng.NextInt(1_000_000_000), retention, maximum, steps);
    });

    /// <summary>A feedback policy: the default, the bundle's, or a random rule table.</summary>
    public static FeedbackPolicy RandomPolicy(PipelineHarness harness, SeededRng rng)
    {
        switch (rng.NextInt(4))
        {
            case 0:
                return FeedbackPolicy.Default;
            case 1:
                return harness.Bundle.FeedbackPolicy;
            default:
                var rules = Enumerable.Range(0, rng.NextInt(8)).Select(_ =>
                {
                    var min = 1 + rng.NextInt(6);
                    var max = rng.NextInt(5) == 0 ? int.MaxValue : min + rng.NextInt(5);
                    Difficulty? difficulty = rng.NextInt(3) == 0 ? (Difficulty)rng.NextInt(3) : null;
                    var pattern = rng.NextInt(3) == 0 ? rng.Pick(ErrorTags.All) : rng.Pick(Patterns);
                    return new FeedbackRule(pattern, min, max, difficulty, (FeedbackType)rng.NextInt(5));
                }).ToArray();
                return new FeedbackPolicy(rules, (FeedbackType)rng.NextInt(5));
        }
    }

    /// <summary>A repeat-count map: missing, zero, negative, small and huge counts.</summary>
    public static ImmutableSortedDictionary<string, int> RandomRepeats(SeededRng rng)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var tag in ErrorTags.All.Append("custom.tag").Append(FeedbackPolicy.NotCommunicatedTag))
        {
            if (rng.NextInt(3) == 0)
            {
                builder[tag] = rng.NextInt(6) switch
                {
                    0 => 0,
                    1 => -rng.NextInt(5),
                    2 => int.MaxValue,
                    _ => 1 + rng.NextInt(8),
                };
            }
        }

        return builder.ToImmutable();
    }
}

/// <summary>An NPC definition, one of its states, the interaction counter and two seeds (the world seed, and a seed for the verdict).</summary>
/// <param name="Definition">The state machine definition.</param>
/// <param name="StateId">The state the NPC is in.</param>
/// <param name="Counter">The interaction counter.</param>
/// <param name="WorldSeed">The world seed.</param>
/// <param name="VerdictSeed">Seeds the verdict generator.</param>
public sealed record NpcCase(NpcDefinition Definition, string StateId, long Counter, ulong WorldSeed, ulong VerdictSeed)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"npc={CanonicalJson.Serialize(Definition)} state={StateId} counter={Counter} worldSeed={WorldSeed} verdictSeed={VerdictSeed}";
    }
}

/// <summary>An NPC definition with one defect and a description of it.</summary>
/// <param name="Definition">The defective definition.</param>
/// <param name="Defect">What was broken.</param>
public sealed record BrokenNpc(NpcDefinition Definition, string Defect)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"defect={Defect} npc={CanonicalJson.Serialize(Definition)}";
    }
}
