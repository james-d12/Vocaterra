using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Decision.Scheduling;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O11: the decision layer. An agent step never throws, lands in a declared state, advances the counter by one, draws variants below the pool size
/// and agrees with an independent model of the documented rules; the same seed gives the same path; a defective definition is rejected with
/// <see cref="InvalidDataException"/> and nothing else. BKT mastery stays in [0, 1] and follows the formula; FSRS keeps stability positive,
/// schedules at least a day ahead and orders Hard before Good before Easy; the feedback plan has at most two items, all from the verdict, and the
/// rule choice matches an independent model of the precedence rules.
/// </summary>
public sealed class DecisionFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O11";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void AgentSteps_LandInDeclaredStates_DrawVariantsInRange_AndAgreeWithTheDocumentedRules()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        DecisionGen.Agent(Harness).Sample(
            c =>
            {
                var machine = Create(c.Definition, c);
                var state = new AgentState(c.Definition.AgentId, c.StateId, c.Counter);
                var verdict = VerdictAssembler.Assemble(VerdictGen.Build(Harness, new SeededRng(c.VerdictSeed)));

                AgentStepResult step;
                try
                {
                    step = machine.Step(state, verdict, c.WorldSeed);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"Step threw {ex.GetType().Name}: {ex.Message}", new { c, verdict });
                }

                var declared = c.Definition.States.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
                if (step.Next.AgentId != state.AgentId || !declared.Contains(step.Next.StateId) || step.Next.InteractionCounter != state.InteractionCounter + 1)
                {
                    throw Determinism.Fail(Oracle, $"The step went to {step.Next} from {state}.", new { c, verdict });
                }

                var expected = ExpectedStep(c.Definition, state, verdict, SeededRng.ForInteraction(c.WorldSeed, state.AgentId, state.InteractionCounter));
                if (!step.Equals(expected))
                {
                    throw Determinism.Fail(Oracle, $"The step {CanonicalJson.Serialize(step)} differs from the documented rules {CanonicalJson.Serialize(expected)}.", new { c, verdict });
                }

                var pools = (c.Definition.VariantPools ?? []).ToDictionary(static p => p.Id, static p => p.Count, StringComparer.Ordinal);
                foreach (var action in step.Actions)
                {
                    if (action.VariantIndex is { } index && (index < 0 || !pools.Values.Any(count => index < count)))
                    {
                        throw Determinism.Fail(Oracle, $"Variant index {index} is outside every pool.", new { c, verdict });
                    }
                }

                // The same seed gives the same step, from a machine rebuilt from the definition's own JSON and from an explicit generator.
                Determinism.AssertSame(
                    Oracle,
                    "AgentStateMachine.Step",
                    new { c, verdict },
                    () => step,
                    () => AgentStateMachine.FromJson(CanonicalJson.Serialize(c.Definition)).Step(state, verdict, SeededRng.ForInteraction(c.WorldSeed, state.AgentId, state.InteractionCounter)));

                // A whole conversation: eight steps with eight verdicts, replayed, give the same path.
                var path = Walk(machine, state, c);
                Determinism.AssertSame(Oracle, "A conversation path", new { c }, () => path, () => Walk(Create(c.Definition, c), state, c));
                foreach (var visited in path)
                {
                    if (!declared.Contains(visited.StateId))
                    {
                        throw Determinism.Fail(Oracle, $"The conversation visited the undeclared state '{visited.StateId}'.", new { c });
                    }
                }

                diagnostics.Count(step.Actions.Any(static a => a.VariantIndex is not null) ? "step:drew-variant" : "step:no-variant");
                diagnostics.Count(machine.Definition.States.Any(s => s.Id == c.StateId && s.Transitions.Count == 0) ? "state:no-transitions" : "state:transitions");
                diagnostics.Count(step.Next.StateId == c.StateId ? "step:stayed" : "step:moved");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("agent diagnostics:\n" + diagnostics.Report());
        foreach (var key in new[] { "step:drew-variant", "step:no-variant", "step:stayed", "step:moved" })
        {
            Assert.True(diagnostics.Get(key) > iterations / 100, $"The generator no longer reaches {key}.\n{diagnostics.Report()}");
        }
    }

    [Fact]
    public void DefectiveAgentDefinitions_AreRejectedWithInvalidDataException_AndNothingElse()
    {
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        DecisionGen.BrokenAgent().Sample(
            broken =>
            {
                try
                {
                    _ = new AgentStateMachine(broken.Definition);
                }
                catch (InvalidDataException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"A definition with {broken.Defect} threw {ex.GetType().Name} instead of InvalidDataException: {ex.Message}", broken);
                }

                throw Determinism.Fail(Oracle, $"A definition with {broken.Defect} was accepted.", broken);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    [Fact]
    public void LearnerModel_FollowsBkt_StaysInRange_AndPicksContentDeterministically()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var gen = Gen.Select(DecisionGen.Bkt, Gen.ULong, DecisionGen.Probability, static (parameters, seed, p) => (parameters, seed, p));

        gen.Sample(
            ((BktParameters Parameters, ulong Seed, double P) generated) =>
            {
                var (parameters, seed, p) = generated;
                var input = new { parameters, seed, p };
                var rng = new SeededRng(seed);
                CheckBktStep(parameters, p, input);

                var model = LearnerModel.New(parameters);
                var expected = new Dictionary<string, (double PKnown, int Attempts, int Errors)>(StringComparer.Ordinal);
                var updates = 1 + rng.NextInt(6);
                for (var step = 0; step < updates; step++)
                {
                    var verdict = VerdictAssembler.Assemble(VerdictGen.Build(Harness, rng));
                    var exercised = Enumerable.Range(0, rng.NextInt(4)).Select(_ => rng.Pick(["intent.buy_item", "intent.ask_price", "vocab", "alpha", "conj", "alpha"])).ToArray();
                    var before = CanonicalJson.Serialize(model);
                    var next = model.Update(verdict, exercised);
                    if (!string.Equals(CanonicalJson.Serialize(model), before, StringComparison.Ordinal))
                    {
                        throw Determinism.Fail(Oracle, "LearnerModel.Update changed the model it was called on.", input);
                    }

                    ApplyModel(expected, parameters, verdict, exercised);
                    CheckLearner(next, expected, input);
                    model = next;
                }

                diagnostics.Count("learner:" + (model.Skills.Count > 0 ? "skills" : "no-skills"));
                CheckContentPicker(model, rng, input);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Learner diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("learner:skills") > iterations / 4, $"Most runs must touch skills.\n{diagnostics.Report()}");
    }

    [Fact]
    public void Fsrs_KeepsStabilityPositive_SchedulesAheadAndOrdersTheRatings()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        DecisionGen.Script.Sample(
            script =>
            {
                var parameters = script.Parameters;
                var now = new StoredTimestamp(script.Start);
                var card = ReviewCard.New("item", now);
                var first = true;
                foreach (var step in script.Steps)
                {
                    now = first ? now : now.AddSeconds(step.DeltaSeconds);
                    first = false;
                    ReviewCard next;
                    try
                    {
                        next = Fsrs.Review(card, step.Rating, now, parameters);
                    }
                    catch (Exception ex)
                    {
                        throw Determinism.Fail(Oracle, $"Fsrs.Review threw {ex.GetType().Name}: {ex.Message}", new { script, step, card });
                    }

                    CheckReview(card, next, step.Rating, now, parameters, script);
                    CheckRatingOrder(card, now, parameters, script, diagnostics);
                    card = next;
                }

                Determinism.AssertSame(Oracle, "A review script", script, () => Replay(script), () => Replay(script));
                diagnostics.Count("script:reviews:" + Math.Min(script.Steps.Count, 10));
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("FSRS diagnostics:\n" + diagnostics.Report());
        Assert.True(
            diagnostics.Get("order:strict") > 0 && diagnostics.Get("order:capped") > 0 && diagnostics.Get("order:new-card") > 0,
            $"The strict, the capped and the new-card interval orderings must all be reached.\n{diagnostics.Report()}");
    }

    [Fact]
    public void RatingMapper_NeverLowersTheRating_WhenCorrectnessRises()
    {
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var gen = Gen.Select(Gen.ULong, Gen.Double[0.0, 1.0], Gen.Double[0.0, 1.0], static (seed, a, b) => (seed, a, b));

        gen.Sample(
            ((ulong Seed, double A, double B) input) =>
            {
                var verdict = VerdictAssembler.Assemble(VerdictGen.Build(Harness, new SeededRng(input.Seed)));
                var low = verdict with { Correctness = Math.Min(input.A, input.B) };
                var high = verdict with { Correctness = Math.Max(input.A, input.B) };
                var lowRating = RatingMapper.FromVerdict(low);
                var highRating = RatingMapper.FromVerdict(high);
                if (!verdict.Communicated && (lowRating != Rating.Again || highRating != Rating.Again))
                {
                    throw Determinism.Fail(Oracle, "A verdict that was not communicated must be rated Again.", new { verdict });
                }

                if (verdict.Communicated && (highRating < lowRating || lowRating == Rating.Again || highRating == Rating.Again))
                {
                    throw Determinism.Fail(Oracle, $"Correctness {low.Correctness} rated {lowRating} but {high.Correctness} rated {highRating}.", new { verdict });
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    [Fact]
    public void FeedbackPlans_HaveAtMostTwoItems_FromTheVerdict_AndRulesFollowTheirPrecedence()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        Gen.ULong.Sample(
            seed =>
            {
                var rng = new SeededRng(seed);
                var policy = DecisionGen.RandomPolicy(Harness, rng);
                var verdict = VerdictAssembler.Assemble(VerdictGen.Build(Harness, rng));
                var repeats = DecisionGen.RandomRepeats(rng);
                var difficulty = (Difficulty)rng.NextInt(3);
                var info = new { seed, policy, verdict, repeats, difficulty };

                FeedbackPlan plan;
                try
                {
                    plan = policy.Plan(verdict, repeats, difficulty);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"Plan threw {ex.GetType().Name}: {ex.Message}", info);
                }

                CheckPlan(plan, policy, verdict, repeats, difficulty, info);
                foreach (var tag in verdict.Errors.Select(static e => e.Tag).Concat(["custom.tag"]).Distinct(StringComparer.Ordinal))
                {
                    var count = rng.NextInt(8) - 1;
                    var chosen = policy.Select(tag, count, difficulty);
                    var model = ExpectedRule(policy, tag, count, difficulty);
                    if (!ReferenceEquals(chosen, model) && !Equals(chosen, model))
                    {
                        throw Determinism.Fail(Oracle, $"Select('{tag}', {count}, {difficulty}) chose {chosen} but the precedence rules choose {model}.", info);
                    }
                }

                Determinism.AssertSame(Oracle, "FeedbackPolicy.Plan", info, () => plan, () => FeedbackPolicy.FromJson(CanonicalJson.Serialize(policy)).Plan(verdict, repeats, difficulty));
                diagnostics.Count(verdict.Communicated ? "plan:communicated" : "plan:not-communicated");
                diagnostics.Count("plan:items:" + plan.Items.Count);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Feedback diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("plan:items:2") > 0 && diagnostics.Get("plan:items:0") > 0 && diagnostics.Get("plan:not-communicated") > 0, $"The generator must reach plans of every size.\n{diagnostics.Report()}");
    }

    // ---- agent ------------------------------------------------------------------------------------------------------------------------

    private static AgentStateMachine Create(AgentDefinition definition, object input)
    {
        try
        {
            return new AgentStateMachine(definition);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"A valid definition was rejected with {ex.GetType().Name}: {ex.Message}", input);
        }
    }

    // Eight verdicts in a row, stepping the machine each time with the world seed; returns the state after each step.
    private static List<AgentState> Walk(AgentStateMachine machine, AgentState start, AgentCase c)
    {
        var rng = new SeededRng(c.VerdictSeed);
        var state = start;
        var path = new List<AgentState>();
        for (var i = 0; i < 8; i++)
        {
            var verdict = VerdictAssembler.Assemble(VerdictGen.Build(Harness, rng));
            state = machine.Step(state, verdict, c.WorldSeed).Next;
            path.Add(state);
        }

        return path;
    }

    // The rules of AgentStateMachine.Step restated: the first transition whose conditions all hold, else the fallback, else stay; one draw per
    // pooled action, in action order.
    private static AgentStepResult ExpectedStep(AgentDefinition definition, AgentState state, Verdict verdict, SeededRng rng)
    {
        var current = definition.States.Single(s => s.Id == state.StateId);
        var target = state.StateId;
        IReadOnlyList<AgentActionDefinition> actions = [];
        var matched = false;
        foreach (var transition in current.Transitions)
        {
            if (ConditionHolds(transition.On, verdict))
            {
                target = transition.To;
                actions = transition.Actions;
                matched = true;
                break;
            }
        }

        if (!matched && current.Fallback is { } fallback)
        {
            target = fallback.To ?? state.StateId;
            actions = fallback.Actions;
        }

        var pools = (definition.VariantPools ?? []).ToDictionary(static p => p.Id, static p => p.Count, StringComparer.Ordinal);
        var steps = actions.Select(a => new AgentAction(a.DialogueIntent, a.HostEvent, a.VariantPoolId is { } id ? rng.NextInt(pools[id]) : null)).ToArray();
        return new AgentStepResult(new AgentState(state.AgentId, target, state.InteractionCounter + 1), steps);
    }

    private static bool ConditionHolds(AgentCondition condition, Verdict verdict)
    {
        if (condition.Intent is { } intent && intent != verdict.Intent.ToString())
        {
            return false;
        }

        if (condition.Communicated is { } communicated && communicated != verdict.Communicated)
        {
            return false;
        }

        if (condition.AnyErrorTag is { } any && !verdict.Errors.Any(e => any.Any(pattern => PatternMatches(pattern, e.Tag))))
        {
            return false;
        }

        return condition.NoErrorTag is not { } none || !verdict.Errors.Any(e => none.Any(pattern => PatternMatches(pattern, e.Tag)));
    }

    private static bool PatternMatches(string pattern, string tag)
    {
        if (pattern == "*")
        {
            return true;
        }

        return pattern.EndsWith(".*", StringComparison.Ordinal)
            ? ErrorTags.Family(tag) == pattern[..^2]
            : pattern == tag;
    }

    // ---- BKT and learner ------------------------------------------------------------------------------------------------------------

    private static double ModelStep(BktParameters q, double p, bool correct)
    {
        var evidence = correct ? (p * (1 - q.Slip)) + ((1 - p) * q.Guess) : (p * q.Slip) + ((1 - p) * (1 - q.Guess));
        var numerator = correct ? p * (1 - q.Slip) : p * q.Slip;
        var posterior = evidence > 0 ? numerator / evidence : p;
        return Math.Clamp(posterior + ((1 - posterior) * q.Learn), 0.0, 1.0);
    }

    private static void CheckBktStep(BktParameters q, double p, object input)
    {
        foreach (var correct in new[] { true, false })
        {
            var next = q.Step(p, correct);
            if (!(next >= 0.0 && next <= 1.0) || Math.Abs(next - ModelStep(q, p, correct)) > 1e-12)
            {
                throw Determinism.Fail(Oracle, $"Step({p}, {correct}) = {next}, the formula gives {ModelStep(q, p, correct)}.", input);
            }
        }

        // With slip + guess <= 1 a correct answer never lowers the posterior and a wrong one never raises it. The learning transition that follows
        // adds (1 - posterior) * learn, so the full step is only monotone for a correct answer; for a wrong one the posterior alone is. (The bound
        // keeps a margin of 1e-9: at slip 1 and guess 1e-71 the sum is 1 in floating point but the real sum is not, and the direction flips.)
        if (q.Slip + q.Guess <= 1.0 - 1e-9)
        {
            if (q.Step(p, true) < p - 1e-12)
            {
                throw Determinism.Fail(Oracle, $"A correct answer lowered P(known) from {p} to {q.Step(p, true)}.", input);
            }

            var withoutLearning = new BktParameters(q.PriorKnown, 0.0, q.Slip, q.Guess);
            if (withoutLearning.Step(p, false) > p + 1e-12)
            {
                throw Determinism.Fail(Oracle, $"A wrong answer raised the posterior from {p} to {withoutLearning.Step(p, false)}.", input);
            }

            if (q.Step(p, false) > q.Step(p, true) + 1e-12)
            {
                throw Determinism.Fail(Oracle, $"A wrong answer ({q.Step(p, false)}) left the learner knowing more than a right one ({q.Step(p, true)}).", input);
            }
        }
    }

    private static void ApplyModel(Dictionary<string, (double PKnown, int Attempts, int Errors)> skills, BktParameters q, Verdict verdict, IReadOnlyList<string> exercised)
    {
        var families = verdict.Errors.Select(static e => ErrorTags.Family(e.Tag)).ToHashSet(StringComparer.Ordinal);
        foreach (var skill in families.Concat(exercised).Distinct(StringComparer.Ordinal).OrderBy(static s => s, StringComparer.Ordinal))
        {
            var wrong = families.Contains(skill);
            var (p, attempts, errors) = skills.TryGetValue(skill, out var known) ? known : (q.PriorKnown, 0, 0);
            skills[skill] = (ModelStep(q, p, !wrong), attempts + 1, errors + (wrong ? 1 : 0));
        }
    }

    private static void CheckLearner(LearnerModel model, Dictionary<string, (double PKnown, int Attempts, int Errors)> expected, object input)
    {
        if (model.Skills.Count != expected.Count)
        {
            throw Determinism.Fail(Oracle, $"The model tracks {model.Skills.Count} skills, expected {expected.Count}.", input);
        }

        for (var i = 0; i < model.Skills.Count; i++)
        {
            var skill = model.Skills[i];
            if (i > 0 && string.CompareOrdinal(model.Skills[i - 1].SkillId, skill.SkillId) >= 0)
            {
                throw Determinism.Fail(Oracle, $"Skills are not strictly ordinal-sorted: '{model.Skills[i - 1].SkillId}' before '{skill.SkillId}'.", input);
            }

            if (!expected.TryGetValue(skill.SkillId, out var want)
                || skill.Attempts != want.Attempts
                || skill.Errors != want.Errors
                || !(skill.PKnown >= 0.0 && skill.PKnown <= 1.0)
                || Math.Abs(skill.PKnown - want.PKnown) > 1e-12)
            {
                throw Determinism.Fail(Oracle, $"Skill '{skill.SkillId}' is {skill}, expected p={(expected.TryGetValue(skill.SkillId, out var w) ? w.PKnown : double.NaN)}.", input);
            }
        }
    }

    // The next item is one of the items; a due card of an available item beats mastery; otherwise the weakest skill wins; ties go to the smaller id.
    private static void CheckContentPicker(LearnerModel model, SeededRng rng, object input)
    {
        var skillPool = new[] { "intent.buy_item", "intent.ask_price", "vocab", "alpha", "conj", "unseen" };
        var items = Enumerable.Range(0, 1 + rng.NextInt(6)).Select(i => new ContentItem("item" + rng.NextInt(8), [.. Enumerable.Range(0, rng.NextInt(3)).Select(_ => rng.Pick(skillPool))])).DistinctBy(static i => i.ItemId).ToArray();
        var now = new StoredTimestamp(1_700_000_000);
        var cards = Enumerable.Range(0, rng.NextInt(5)).Select(_ =>
        {
            var due = now.AddSeconds((rng.NextInt(9) - 4) * 86_400L);
            return new ReviewCard("item" + rng.NextInt(10), 3.0, 5.0, now, due, 1, 0);
        }).ToArray();

        var picked = ContentPicker.Next(model, items, cards, now);
        if (!items.Any(i => i.ItemId == picked))
        {
            throw Determinism.Fail(Oracle, $"ContentPicker chose '{picked}', which is not an item.", input);
        }

        if (!string.Equals(picked, ContentPicker.Next(model, items, cards, now), StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, "ContentPicker is not deterministic.", input);
        }

        var due = cards.Where(c => c.Due <= now && items.Any(i => i.ItemId == c.ItemId)).OrderBy(static c => c.Due.UnixSeconds).ThenBy(static c => c.ItemId, StringComparer.Ordinal).ToArray();
        var expected = due.Length > 0
            ? due[0].ItemId
            : items.OrderBy(i => Math.Round(i.SkillIds.Count == 0 ? 1.0 : i.SkillIds.Min(model.PKnown), 9, MidpointRounding.AwayFromZero)).ThenBy(static i => i.ItemId, StringComparer.Ordinal).First().ItemId;
        if (!string.Equals(picked, expected, StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, $"ContentPicker chose '{picked}' but the documented rules choose '{expected}'.", input);
        }
    }

    // ---- FSRS -----------------------------------------------------------------------------------------------------------------------

    private static string Replay(ReviewScript script)
    {
        var now = new StoredTimestamp(script.Start);
        var card = ReviewCard.New("item", now);
        var first = true;
        foreach (var step in script.Steps)
        {
            now = first ? now : now.AddSeconds(step.DeltaSeconds);
            first = false;
            card = Fsrs.Review(card, step.Rating, now, script.Parameters);
        }

        return CanonicalJson.Serialize(card);
    }

    private static void CheckReview(ReviewCard previous, ReviewCard next, Rating rating, StoredTimestamp now, FsrsParameters parameters, ReviewScript script)
    {
        object Info() => new { script, previous, next, rating, now };
        if (!(next.Stability > 0.0 && double.IsFinite(next.Stability)) || !(next.Difficulty >= 1.0 && next.Difficulty <= 10.0))
        {
            throw Determinism.Fail(Oracle, $"Stability {next.Stability} and difficulty {next.Difficulty} must be positive/finite and in [1, 10].", Info());
        }

        var seconds = now.SecondsUntil(next.Due);
        var days = seconds / 86_400;
        if (seconds % 86_400 != 0 || days < 1 || days > parameters.MaximumIntervalDays || next.Due < now)
        {
            throw Determinism.Fail(Oracle, $"The card is due in {seconds} seconds ({days} days); it must be a whole number of days between 1 and {parameters.MaximumIntervalDays}.", Info());
        }

        if (next.LastReview != now || next.Reps != previous.Reps + 1 || next.Lapses < previous.Lapses || next.Lapses > previous.Lapses + 1)
        {
            throw Determinism.Fail(Oracle, "The review did not record the time, the repetition and at most one lapse.", Info());
        }

        var lapse = !previous.IsNew && rating == Rating.Again;
        if ((next.Lapses == previous.Lapses + 1) != lapse)
        {
            throw Determinism.Fail(Oracle, $"Lapses went from {previous.Lapses} to {next.Lapses} on a {rating} review of a {(previous.IsNew ? "new" : "known")} card.", Info());
        }

        var retrievability = Fsrs.Retrievability(next, now.AddSeconds(86_400L * 3));
        if (!(retrievability > 0.0 && retrievability <= 1.0))
        {
            throw Determinism.Fail(Oracle, $"Retrievability {retrievability} is outside (0, 1].", Info());
        }
    }

    // From the same card at the same time, Hard < Good < Easy in days; once the maximum interval is reached they may tie there.
    private static void CheckRatingOrder(ReviewCard card, StoredTimestamp now, FsrsParameters parameters, ReviewScript script, FuzzDiagnostics diagnostics)
    {
        long Days(Rating rating) => now.SecondsUntil(Fsrs.Review(card, rating, now, parameters).Due) / 86_400;
        var hard = Days(Rating.Hard);
        var good = Days(Rating.Good);
        var easy = Days(Rating.Easy);
        var again = Days(Rating.Again);
        // A reviewed card schedules the three passing ratings together, so Hard < Good < Easy unless the maximum interval caps them. A new card
        // has no learning steps (every rating is at least a day ahead), so its intervals tie at the one-day floor: only Hard <= Good <= Easy holds.
        var capped = easy >= parameters.MaximumIntervalDays;
        var strict = !card.IsNew && !capped;
        var ordered = strict ? hard < good && good < easy : hard <= good && good <= easy && again <= good;
        if (!ordered || again < 1)
        {
            throw Determinism.Fail(Oracle, $"Intervals Again={again}, Hard={hard}, Good={good}, Easy={easy} days (maximum {parameters.MaximumIntervalDays}, {(card.IsNew ? "new" : "reviewed")} card) are not ordered.", new { script, card, now });
        }

        diagnostics.Count(card.IsNew ? "order:new-card" : capped ? "order:capped" : "order:strict");
    }

    // ---- Feedback -------------------------------------------------------------------------------------------------------------------

    private static void CheckPlan(FeedbackPlan plan, FeedbackPolicy policy, Verdict verdict, IReadOnlyDictionary<string, int> repeats, Difficulty difficulty, object info)
    {
        if (plan.Items.Count > FeedbackPolicy.MaxItems)
        {
            throw Determinism.Fail(Oracle, $"The plan has {plan.Items.Count} items, at most {FeedbackPolicy.MaxItems} are allowed.", info);
        }

        if (!verdict.Communicated)
        {
            var first = verdict.Errors.Count > 0 ? verdict.Errors[0] : null;
            var item = plan.Items.SingleOrDefault();
            if (plan.Items.Count != 1 || item!.Type != policy.NotCommunicatedType || item.Tag != (first?.Tag ?? FeedbackPolicy.NotCommunicatedTag))
            {
                throw Determinism.Fail(Oracle, "A verdict that was not communicated must get exactly one item of the not-communicated type quoting the first error.", info);
            }

            return;
        }

        // Every item names an error of the verdict, in the verdict's error order, with that error's expected and actual text.
        var cursor = 0;
        foreach (var item in plan.Items)
        {
            while (cursor < verdict.Errors.Count && !(verdict.Errors[cursor].Tag == item.Tag && verdict.Errors[cursor].Expected == item.Expected && verdict.Errors[cursor].Actual == item.Actual))
            {
                cursor++;
            }

            if (cursor == verdict.Errors.Count)
            {
                throw Determinism.Fail(Oracle, $"The item {item} is not an error of the verdict, or is out of order.", info);
            }

            var count = repeats.TryGetValue(item.Tag, out var seen) && seen >= 1 ? seen : 1;
            if (ExpectedRule(policy, item.Tag, count, difficulty)?.Type != item.Type)
            {
                throw Determinism.Fail(Oracle, $"The item {item} does not have the type of the winning rule.", info);
            }

            cursor++;
        }

        // Errors with a matching rule fill the plan up to the maximum, in order.
        var expected = verdict.Errors
            .Where(e => ExpectedRule(policy, e.Tag, repeats.TryGetValue(e.Tag, out var seen) && seen >= 1 ? seen : 1, difficulty) is not null)
            .Take(FeedbackPolicy.MaxItems)
            .Select(static e => e.Tag)
            .ToArray();
        if (!plan.Items.Select(static i => i.Tag).SequenceEqual(expected))
        {
            throw Determinism.Fail(Oracle, $"The plan covers [{string.Join(", ", plan.Items.Select(static i => i.Tag))}] but the first matching errors are [{string.Join(", ", expected)}].", info);
        }
    }

    // Rule precedence restated: exact tag over family wildcard over *, then the narrower repeat range, then a rule that names the difficulty, then the earlier rule.
    private static FeedbackRule? ExpectedRule(FeedbackPolicy policy, string tag, int repeat, Difficulty difficulty)
    {
        return policy.Rules
            .Select((rule, index) => (rule, index))
            .Where(x => repeat >= x.rule.MinRepeat && repeat <= x.rule.MaxRepeat && (x.rule.Difficulty is null || x.rule.Difficulty == difficulty) && PatternMatches(x.rule.TagPattern, tag))
            .OrderByDescending(static x => x.rule.TagPattern == "*" ? 0 : x.rule.TagPattern.EndsWith(".*", StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(static x => (long)x.rule.MaxRepeat - x.rule.MinRepeat)
            .ThenBy(static x => x.rule.Difficulty is null ? 1 : 0)
            .ThenBy(static x => x.index)
            .Select(static x => x.rule)
            .FirstOrDefault();
    }
}
