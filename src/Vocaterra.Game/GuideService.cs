using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Templates;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// The conversation with the guide, one typed line at a time. A turn evaluates the line on the bundle's <c>guide-open</c> prompt with the
/// core's scripted recognizer (the typed text is the transcript), carries the conversation with <c>ConversationContext.Next</c> (the guide's
/// state, the learner model and the repeat counts), and turns the guide's host events into facts about the world.
/// </summary>
/// <remarks>
/// <para>
/// The core's replies cannot name world facts (CR-003: templates see only the verdict's slots). The guide's answer therefore has two parts: the
/// agent's own reply, which comes from the core and is in its replay record, and the game's own line ("El árbol está a la derecha, a doce
/// bloques."), which this class renders with the bundle's template engine from the bundle's dialogue pools after the core is done. The
/// second line is a pure function of the world, the seed and the interaction counter, but it is <b>outside the core's replay record</b>;
/// CR-003's answer would bring it inside.
/// </para>
/// <para>
/// Nothing here reads the clock or draws a random number of its own: the stored time and the host state hash come from the caller, and the
/// variant of the game's line is drawn from the core's <see cref="SeededRng"/> keyed by the world seed and the interaction counter.
/// </para>
/// </remarks>
public sealed class GuideService
{
    /// <summary>The bundle's prompt for talking to the guide.</summary>
    public const string PromptId = "guide-open";

    /// <summary>The guide's agent id in the bundle.</summary>
    public const string AgentId = "guide";

    /// <summary>The random stream of the game's own lines is keyed by this id (the core's own draws use <see cref="AgentId"/>).</summary>
    private const string HostLineStream = "guide.host";

    private const string FeedbackLinePrefix = "feedback.";
    private const string PointFound = "guide.point.found";
    private const string PointHere = "guide.point.here";
    private const string PointNone = "guide.point.none";
    private const string IdentifyFound = "guide.identify.found";
    private const string IdentifyNone = "guide.identify.none";
    private const string HelpAsk = "guide.help.ask";
    private const string HelpWalk = "guide.help.walk";
    private const string NameRight = "guide.name.correct";
    private const string NameWrong = "guide.name.wrong";
    private const string NameNone = "guide.name.none";

    private readonly ContentBundle _bundle;
    private readonly TypedUtteranceEvaluator _evaluator;
    private readonly WorldFacts _facts;
    private ConversationContext _context;

    /// <summary>Starts the conversation: the guide in its initial state, a learner model with no evidence and no repeat counts.</summary>
    /// <param name="bundle">The content bundle; it must hold the <c>guide</c> agent and the <c>guide-open</c> prompt.</param>
    /// <param name="facts">The world the guide speaks of.</param>
    /// <param name="worldSeed">The world seed, which seeds every random draw with the agent id and the interaction counter.</param>
    /// <param name="start">The stored time before the first turn.</param>
    /// <exception cref="ArgumentException">The bundle has no guide.</exception>
    public GuideService(ContentBundle bundle, WorldFacts facts, ulong worldSeed, StoredTimestamp start)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(facts);
        if (!bundle.Agents.TryGetValue(AgentId, out var machine) || !bundle.Prompts.ContainsKey(PromptId))
        {
            throw new ArgumentException($"The bundle has no agent '{AgentId}' or no prompt '{PromptId}'.", nameof(bundle));
        }

        _bundle = bundle;
        _facts = facts;
        _evaluator = new TypedUtteranceEvaluator(bundle);
        _context = ConversationContext.Create(worldSeed, AgentId, machine.InitialState(), start, LearnerModel.New());
    }

    /// <summary>The context the next turn starts from (the guide's state, the learner model, the repeat counts and the time of the last turn).</summary>
    public ConversationContext Context => _context;

    /// <summary>The guide's current state in its state machine.</summary>
    public AgentState GuideState => _context.AgentState;

    /// <summary>The line the guide says when the dialogue opens (<c>PromptSpec.Text</c> of the prompt).</summary>
    public string OpeningLine => _bundle.Prompts[PromptId].Text ?? string.Empty;

    /// <summary>
    /// Runs one throwaway evaluation so that the first real turn does not pay for the pipeline's first-call costs (JIT, lazy tables) in the
    /// middle of a frame. It changes no state: the conversation, the learner model and the repeat counts stay as they were. Call it while a
    /// loading screen is up.
    /// </summary>
    public void WarmUp()
    {
        _evaluator.EvaluateCountingThisUtterance("hola", _bundle.Prompts[PromptId], _context);
    }

    /// <summary>
    /// Evaluates one typed line and returns the turn. The conversation moves on whatever the verdict: an unclear line also counts, as a
    /// repeated mistake or a not-communicated attempt.
    /// </summary>
    /// <param name="line">What the player typed.</param>
    /// <param name="scene">Where the player is and what the cursor is on.</param>
    /// <param name="now">The stored time of this turn (the game's start time plus its ticks; never the clock).</param>
    /// <param name="hostStateHash">The game's digest of the rest of its state, copied into the replay record (the simulation's state digest).</param>
    public GuideTurn Say(string line, GuideScene scene, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(hostStateHash);
        var prompt = _bundle.Prompts[PromptId];
        var utterance = _context with { Now = now, HostStateHash = hostStateHash };
        var result = _evaluator.EvaluateCountingThisUtterance(line, prompt, utterance);
        _context = utterance.Next(result, now);

        var verdict = result.Verdict;
        var actions = result.Decision.Agent?.Actions ?? [];
        var hostEvents = actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();

        string? feedback = null;
        var replies = new List<string>();
        foreach (var responseLine in result.Response.Lines)
        {
            if (responseLine.IsRecast || responseLine.LineId.StartsWith(FeedbackLinePrefix, StringComparison.Ordinal))
            {
                feedback ??= responseLine.Text;
            }
            else
            {
                replies.Add(responseLine.Text);
            }
        }

        string? worldLine = null;
        GuidePointer? pointer = null;
        string? identified = null;
        if (verdict.Communicated)
        {
            var rng = SeededRng.ForInteraction(utterance.WorldSeed, HostLineStream, utterance.AgentState.InteractionCounter);
            foreach (var hostEvent in hostEvents)
            {
                switch (hostEvent)
                {
                    case GuideEvents.Point:
                        (worldLine, pointer) = Point(verdict, scene, rng);
                        break;
                    case GuideEvents.Identify:
                        (worldLine, identified) = Identify(scene, rng);
                        break;
                    case GuideEvents.Help:
                        worldLine = Render(scene.PointerActive ? HelpWalk : HelpAsk, SlotMap.Empty, rng);
                        break;
                    case GuideEvents.Name:
                        worldLine = Name(verdict, scene, rng);
                        break;
                    default:
                        break;
                }
            }
        }

        return new GuideTurn(
            line,
            verdict.Intent.Value,
            verdict.Communicated,
            verdict.Correctness,
            verdict.Errors.Select(static e => new GuideError(e.Tag, e.Expected, e.Actual, e.Span)).ToList(),
            feedback,
            replies,
            result.Repair?.AgentLine,
            _context.AgentState.StateId,
            hostEvents,
            worldLine,
            pointer,
            identified,
            result.Replay.HostStateHash,
            verdict.Slots);
    }

    private (string Line, GuidePointer? Pointer) Point(Verdict verdict, GuideScene scene, SeededRng rng)
    {
        var place = verdict.Slots.TryGetValue("place", out var lemma) ? lemma : string.Empty;
        var found = place.Length == 0 ? null : _facts.NearestOfNoun(scene.Player, place);
        var slots = SlotMap.Empty.With("place", place);
        if (found is null)
        {
            return (Render(PointNone, slots, rng), null);
        }

        if (found.Blocks == 0)
        {
            return (Render(PointHere, slots, rng), new GuidePointer(place, found.Tile, 0, string.Empty));
        }

        slots = slots
            .With("direction", found.Direction)
            .With("count", found.Blocks.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .With("unit", "bloque");
        return (Render(PointFound, slots, rng), new GuidePointer(place, found.Tile, found.Blocks, found.Direction));
    }

    private (string Line, string? Noun) Identify(GuideScene scene, SeededRng rng)
    {
        var noun = scene.Cursor is { } cursor ? NounAt(cursor) : null;
        if (noun is null)
        {
            return (Render(IdentifyNone, SlotMap.Empty, rng), null);
        }

        return (Render(IdentifyFound, SlotMap.Empty.With("target", noun), rng), noun);
    }

    // The guide's check of a name: the cursor's tile is the expected noun. A name is right when it is that noun and no error was found on it;
    // the core cannot grade against a host value (CR-014), so the game does. The word counts do not depend on this (WordProgress).
    private string Name(Verdict verdict, GuideScene scene, SeededRng rng)
    {
        var target = scene.Cursor is { } cursor ? NounAt(cursor) : null;
        if (target is null)
        {
            return Render(NameNone, SlotMap.Empty, rng);
        }

        var named = verdict.Slots.TryGetValue("object", out var lemma) ? lemma : string.Empty;
        var right = named == target && verdict.Errors.Count == 0;
        return Render(right ? NameRight : NameWrong, SlotMap.Empty.With("target", target), rng);
    }

    private string? NounAt(TilePoint tile) => WorldFacts.NounOfTile(_facts.TileAt(tile));

    private string Render(string poolId, SlotMap slots, SeededRng rng)
    {
        // CR-003 workaround: the game renders its own line from the bundle's pool with the bundle's template engine. The draw is always made,
        // so the number of draws per turn does not depend on whether a pool has one variant or three.
        var pool = _bundle.Dialogue.TryGetPool(poolId, out var found)
            ? found
            : throw new InvalidOperationException($"The bundle has no dialogue pool '{poolId}'.");
        var variant = DialogueSelector.Select(pool, rng);
        return _bundle.TemplateEngine.Render(variant, slots);
    }
}
