using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Templates;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// The conversation with the guide, one typed or spoken line at a time. A turn evaluates the line on the bundle's <c>guide-open</c> prompt:
/// a typed line with the core's scripted recognizer (the typed text is the transcript), a spoken line with Vosk (the clip is decoded once).
/// It carries the conversation with <c>ConversationContext.Next</c> (the guide's state, the learner model and the repeat counts), and turns
/// the guide's host events into facts about the world.
/// </summary>
/// <remarks>
/// <para>
/// A turn evaluates the line twice, both times purely. The first evaluation gives the verdict, the words with their readings (CR-002) and the
/// host events. The host's facts about the world (the place, the direction and distance, the target) and the word skills of the nouns the turn
/// exercises (CR-005) depend on those, so the second evaluation carries them in its context (<c>ConversationContext.Create</c> with
/// <c>hostSlots</c> and <c>extraSkills</c>): they are in the replay record, and the learner is updated once. Only the second result is used.
/// For a spoken line the second evaluation runs over the first one's recognition (<see cref="TypedUtteranceEvaluator.EvaluateRecognized"/>), so
/// Vosk decodes the clip once and both verdicts see the same transcript and confidence.
/// </para>
/// <para>
/// <b>Threads (P5).</b> A turn is a pure function of its inputs (the line or clip, the scene, the stored time and the host state hash, all
/// fixed when the player submits). The game runs turns one at a time on its worker thread, which is the only thread that calls
/// <see cref="Say"/> and <see cref="SaySpoken"/>; the properties that report the conversation may be read from the game thread between turns.
/// </para>
/// <para>
/// The guide's world line ("El árbol está a la derecha, a doce bloques.") is still rendered by the game from the bundle's dialogue pools with
/// the bundle's template engine. The core cannot pick it: a transition is chosen by the verdict alone, and a template that names a host slot
/// the host did not supply fails the whole evaluation, so the three cases (here, found, none) cannot be three core templates (CR-016). The
/// facts the line is rendered from are host slots of the second evaluation, so the record holds the inputs of the line.
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

    /// <summary>The bundle's prompt for the review question ("¿Qué es esto?"): the answer to a review is evaluated on it.</summary>
    public const string ReviewPromptId = "guide-review";

    /// <summary>The guide's agent id in the bundle.</summary>
    public const string AgentId = "guide";

    /// <summary>The random stream of the game's own lines is keyed by this id (the core's own draws use <see cref="AgentId"/>).</summary>
    private const string HostLineStream = "guide.host";

    /// <summary>The skill id prefix of a catalogue noun's learner skill (CR-005): <c>word.árbol</c>.</summary>
    private const string WordSkillPrefix = "word.";

    private const string FeedbackLinePrefix = "feedback.";
    private const string PointFound = "guide.point.found";
    private const string PointHere = "guide.point.here";
    private const string PointNone = "guide.point.none";
    private const string PointDeep = "guide.point.deep";
    private const string IdentifyFound = "guide.identify.found";
    private const string IdentifyNone = "guide.identify.none";
    private const string CraftRecipe = "guide.craft.recipe";
    private const string CraftStationUse = "guide.craft.station.use";
    private const string CraftStationNone = "guide.craft.station.none";
    private const string CraftLackOne = "guide.craft.lack.one";
    private const string CraftLackMany = "guide.craft.lack.many";
    private const string CraftReady = "guide.craft.ready";
    private const string CraftNone = "guide.craft.none";
    private const string CraftPartMass = "guide.craft.part.mass";
    private const string CraftPartCount = "guide.craft.part.count";
    private const string FindSource = "guide.find.source";
    private const string HaveOne = "guide.find.have.one";
    private const string HaveMany = "guide.find.have.many";
    private const string HaveMass = "guide.find.have.mass";
    private const string BlockUnit = "bloque";
    private const string HelpAsk = "guide.help.ask";
    private const string HelpWalk = "guide.help.walk";
    private const string NameRight = "guide.name.correct";
    private const string NameWrong = "guide.name.wrong";
    private const string NameNone = "guide.name.none";
    private const string ReviewAsk = "guide.review.ask";
    private const string ReviewStream = "guide.review";

    private readonly ContentBundle _bundle;
    private readonly TypedUtteranceEvaluator _evaluator;
    private readonly IGuideFacts _worldFacts;
    private readonly NounCatalog _nouns;
    private readonly RecipeBook _recipes;
    private volatile ConversationContext _context;
    private volatile SpokenUtteranceEvaluator? _spoken;
    private IGuideFacts _facts;

    /// <summary>Starts the conversation: the guide in its initial state, a learner model with no evidence and no repeat counts.</summary>
    /// <param name="bundle">The content bundle; it must hold the <c>guide</c> agent and the <c>guide-open</c> prompt.</param>
    /// <param name="facts">The world the guide speaks of.</param>
    /// <param name="nouns">The noun catalogue of the same bundle: the nouns a turn exercises get their learner skills.</param>
    /// <param name="worldSeed">The world seed, which seeds every random draw with the agent id and the interaction counter.</param>
    /// <param name="start">The stored time before the first turn.</param>
    /// <param name="recipes">The bundle's recipes, for the crafting answers; read from the bundle when <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">The bundle has no guide.</exception>
    public GuideService(ContentBundle bundle, IGuideFacts facts, NounCatalog nouns, ulong worldSeed, StoredTimestamp start, RecipeBook? recipes = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(nouns);
        if (!bundle.Agents.TryGetValue(AgentId, out var machine) || !bundle.Prompts.ContainsKey(PromptId) || !bundle.Prompts.ContainsKey(ReviewPromptId))
        {
            throw new ArgumentException($"The bundle has no agent '{AgentId}' or no prompt '{PromptId}' or '{ReviewPromptId}'.", nameof(bundle));
        }

        _bundle = bundle;
        _worldFacts = facts;
        _facts = facts;
        _nouns = nouns;
        _recipes = recipes ?? RecipeBook.Load(bundle, nouns);
        _evaluator = new TypedUtteranceEvaluator(bundle);
        _context = ConversationContext.Create(worldSeed, AgentId, machine.InitialState(), start, LearnerModel.New(), new Dictionary<string, int>(StringComparer.Ordinal), SlotMap.Empty, []);
    }

    /// <summary>Whether spoken lines can be evaluated (<see cref="AttachSpeech"/> was called).</summary>
    public bool CanHear => _spoken is not null;

    /// <summary>
    /// Gives the guide the means to evaluate spoken lines. Call it once, from the thread that will call <see cref="SaySpoken"/>, after the
    /// recognizer has loaded.
    /// </summary>
    /// <param name="evaluator">The spoken-line evaluator over the loaded recognizer.</param>
    public void AttachSpeech(SpokenUtteranceEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        _spoken = evaluator;
    }

    /// <summary>The context the next turn starts from (the guide's state, the learner model, the repeat counts and the time of the last turn).</summary>
    public ConversationContext Context => _context;

    /// <summary>The guide's current state in its state machine.</summary>
    public AgentState GuideState => _context.AgentState;

    /// <summary>The line the guide says when the dialogue opens (<c>PromptSpec.Text</c> of the prompt).</summary>
    public string OpeningLine => _bundle.Prompts[PromptId].Text ?? string.Empty;

    /// <summary>
    /// The guide's review question, drawn from the bundle's <c>guide.review.ask</c> pool: the variant depends on the world seed and the
    /// interaction counter, so the same conversation gives the same question. It is a host line outside the core's replay record: the core
    /// cannot open an agent turn itself (CR-015).
    /// </summary>
    public string ReviewQuestion()
    {
        var rng = SeededRng.ForInteraction(_context.WorldSeed, ReviewStream, _context.AgentState.InteractionCounter);
        return Render(ReviewAsk, SlotMap.Empty, rng);
    }

    /// <summary>
    /// Runs one throwaway evaluation so that the first real turn does not pay for the pipeline's first-call costs (JIT, lazy tables) in the
    /// middle of a frame. It changes no state: the conversation, the learner model and the repeat counts stay as they were. Call it while a
    /// loading screen is up.
    /// </summary>
    public void WarmUp()
    {
        _evaluator.Evaluate("hola", _bundle.Prompts[PromptId], _context);
    }

    /// <summary>
    /// Runs one throwaway spoken evaluation of half a second of silence, so that the first real clip does not pay for Vosk's first decode and
    /// the pipeline's first spoken call. It changes no state. Call it from the speech thread, after <see cref="AttachSpeech"/>.
    /// </summary>
    public void WarmUpSpeech()
    {
        var spoken = _spoken ?? throw new InvalidOperationException("No speech is attached.");
        spoken.Trace(PcmClip.FromSamples(new short[AudioClips.SampleRate / 2]), _bundle.Prompts[PromptId], _context);
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
        return Take(line, null, scene, now, hostStateHash);
    }

    /// <summary>
    /// Recognizes and evaluates one spoken line and returns the turn, whose <see cref="GuideTurn.Line"/> is what the recognizer heard. The
    /// recognizer decodes the clip once; the second evaluation reuses its output. The conversation moves on whatever the verdict.
    /// </summary>
    /// <param name="clip">The recorded or synthesized clip, 16 kHz mono (see <see cref="AudioClips.Prepare"/>).</param>
    /// <param name="scene">Where the player is and what the cursor is on.</param>
    /// <param name="now">The stored time of this turn.</param>
    /// <param name="hostStateHash">The game's digest of the rest of its state.</param>
    /// <exception cref="InvalidOperationException">No speech is attached (<see cref="AttachSpeech"/>).</exception>
    public GuideTurn SaySpoken(PcmClip clip, GuideScene scene, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Take(null, clip, scene, now, hostStateHash);
    }

    private GuideTurn Take(string? typed, PcmClip? clip, GuideScene scene, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(hostStateHash);
        var prompt = _bundle.Prompts[scene.Review ? ReviewPromptId : PromptId];
        var utterance = _context with { Now = now, HostStateHash = hostStateHash };
        _facts = scene.Facts ?? _worldFacts; // the world as it was when the player spoke, not as it is when the worker gets to the turn

        // The first evaluation finds the verdict, the words and the host events; the facts and the skills come from them. A spoken line is
        // decoded here, once: the recognition is kept for the second evaluation.
        AsrResult? recognition = null;
        PipelineResult first;
        string line;
        if (clip is null)
        {
            line = typed!;
            first = _evaluator.Evaluate(line, prompt, utterance);
        }
        else
        {
            var spoken = _spoken ?? throw new InvalidOperationException("No speech is attached to the guide.");
            var trace = spoken.Trace(clip, prompt, utterance);
            first = trace.Result;
            recognition = trace.Asr;
            line = recognition.Transcript;
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        string? worldLine = null;
        GuidePointer? pointer = null;
        string? identified = null;
        var naming = NameCheck.None;
        string? nameTarget = null;
        if (first.Verdict.Communicated)
        {
            var rng = SeededRng.ForInteraction(utterance.WorldSeed, HostLineStream, utterance.AgentState.InteractionCounter);
            foreach (var hostEvent in HostEventsOf(first))
            {
                switch (hostEvent)
                {
                    case GuideEvents.Point:
                        (worldLine, pointer) = Point(first.Verdict, scene, rng, facts);
                        break;
                    case GuideEvents.Craft:
                        worldLine = Craft(first.Verdict, scene, rng, facts);
                        break;
                    case GuideEvents.Find:
                        (worldLine, pointer) = Find(first.Verdict, scene, rng, facts);
                        break;
                    case GuideEvents.Identify:
                        (worldLine, identified) = Identify(scene, rng, facts);
                        break;
                    case GuideEvents.Help:
                        worldLine = Render(scene.PointerActive ? HelpWalk : HelpAsk, SlotMap.Empty, rng);
                        break;
                    case GuideEvents.Name:
                        (worldLine, naming, nameTarget) = Name(first.Verdict, scene, rng, facts);
                        break;
                    default:
                        break;
                }
            }
        }

        // The second evaluation carries the host's facts and the word skills in its context; the learner is updated once, in it. It is needed
        // because the core takes host slots only before an evaluation, and the facts depend on the nouns the first one found in the line.
        // Its digest covers those slots and skills (HostDigest, the core's contract); without them the context keeps the bare digest, as the
        // core does when no slots are given.
        var skills = first.Verdict.Communicated ? WordSkillsOf(GuideWord.From(first)) : [];
        var created = ConversationContext.Create(
            utterance.WorldSeed,
            utterance.AgentId,
            utterance.AgentState,
            now,
            utterance.Learner,
            utterance.RepeatCountsByTag,
            SlotMap.From(facts),
            skills);
        var digest = facts.Count == 0 && skills.Count == 0 ? hostStateHash : HostDigest.Of(hostStateHash, facts, skills);
        var carried = created with { HostStateHash = digest };
        var result = recognition is null
            ? _evaluator.Evaluate(line, prompt, carried)
            : _evaluator.EvaluateRecognized(recognition, clip!, prompt, carried);
        _context = carried.Next(result, now);

        var verdict = result.Verdict;
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
            HostEventsOf(result),
            worldLine,
            pointer,
            identified,
            result.Replay.HostStateHash,
            verdict.Slots,
            naming,
            nameTarget,
            GuideWord.From(result),
            clip is null ? InputKind.Typed : InputKind.Spoken,
            recognition is null ? null : RecognitionConfidence(recognition));
    }

    // How sure the recognizer was of the words: the mean of Vosk's per-word confidences (the result's own confidence is 1.0 for a single-best decode).
    private static double RecognitionConfidence(AsrResult recognition) =>
        recognition.Words.Count == 0 ? recognition.Confidence : recognition.Words.Average(static w => w.Confidence);

    private static List<string> HostEventsOf(PipelineResult result)
    {
        var actions = result.Decision.Agent?.Actions ?? [];
        return actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();
    }

    // The learner skill of each catalogue noun the line's words are read as (CR-005): one per noun, in ordinal order.
    private List<string> WordSkillsOf(IReadOnlyList<GuideWord> words)
    {
        var skills = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var noun = _nouns.ForReading(word.Lemma, word.Pos);
            if (noun is not null)
            {
                skills.Add(WordSkillPrefix + noun.Lemma);
            }
        }

        return [.. skills];
    }

    private (string Line, GuidePointer? Pointer) Point(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var place = verdict.Slots.TryGetValue("place", out var lemma) ? lemma : string.Empty;
        var found = place.Length == 0 ? null : _facts.NearestOfNoun(scene.Player, place);

        // A thing with no tile in the world (the axe) that the player carries: say so instead of "there is none here".
        if (found is null && CountCarried(place, scene) > 0)
        {
            return (Have(place, scene, rng, facts), null);
        }

        return PointAt(place, found, rng, facts);
    }

    // The point-at line for the nearest tile of a noun (or the here / none line), with the arrow.
    private (string Line, GuidePointer? Pointer) PointAt(string place, GuideNearest? found, SeededRng rng, Dictionary<string, string> facts)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["place"] = place };
        if (found is null)
        {
            Merge(facts, values);

            // An ore is underground: none in the snapshot means it lies far below, not that it is absent.
            var pool = _nouns.ForLemma(place) is { Deep: true } ? PointDeep : PointNone;
            return (Render(pool, SlotMap.From(values), rng), null);
        }

        if (found.Blocks == 0)
        {
            Merge(facts, values);
            return (Render(PointHere, SlotMap.From(values), rng), new GuidePointer(place, found.Cell, 0, string.Empty));
        }

        values["direction"] = found.Direction;
        values["count"] = found.Blocks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["unit"] = BlockUnit;
        Merge(facts, values);
        return (Render(PointFound, SlotMap.From(values), rng), new GuidePointer(place, found.Cell, found.Blocks, found.Direction));
    }

    // How many of a catalogue noun's items the player carries (zero when the noun is unknown or the scene has no inventory).
    private int CountCarried(string lemma, GuideScene scene)
    {
        if (scene.Inventory is not { } inventory || _nouns.ForLemma(lemma) is not { } noun)
        {
            return 0;
        }

        return noun.Items.Sum(inventory.Count);
    }

    // "Tienes un hacha." / "Tienes dos hachas." / "Tienes tres bloques de madera.": what the player already carries.
    private string Have(string lemma, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var noun = _nouns.ForLemma(lemma)!;
        var count = CountCarried(lemma, scene);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["thing"] = lemma,
            ["count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["unit"] = BlockUnit,
        };
        Merge(facts, values);
        var pool = noun.Mass ? HaveMass : count == 1 ? HaveOne : HaveMany;
        return Render(pool, SlotMap.From(values), rng);
    }

    // The player asks where a material is or where it comes from: what it comes from and the nearest of that (its own tile when it is one).
    private (string Line, GuidePointer? Pointer) Find(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var material = verdict.Slots.TryGetValue("material", out var lemma) ? lemma : string.Empty;
        if (CountCarried(material, scene) > 0)
        {
            return (Have(material, scene, rng, facts), null);
        }

        var noun = _nouns.ForLemma(material);
        IReadOnlyList<string> sources = noun is { Sources.Count: > 0 } ? noun.Sources : [material];
        var bestSource = sources[0];
        GuideNearest? best = null;
        foreach (var source in sources)
        {
            var near = _facts.NearestOfNoun(scene.Player, source);
            if (near is not null && (best is null || near.Distance2 < best.Distance2))
            {
                best = near;
                bestSource = source;
            }
        }

        var lines = new List<string>();
        if (noun is not null && !string.Equals(bestSource, material, StringComparison.Ordinal))
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["material"] = material, ["source"] = bestSource, ["many"] = "2" };
            facts["thing"] = material;
            facts["source"] = bestSource;
            lines.Add(Render(FindSource, SlotMap.From(values), rng));
        }

        var (pointLine, pointer) = PointAt(bestSource, best, rng, facts);
        lines.Add(pointLine);
        return (string.Join(' ', lines), pointer);
    }


    // How to make something: the ingredients with their numbers, the station, and what the player still lacks.
    private string Craft(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var lemma = verdict.Slots.TryGetValue("item", out var asked) ? asked : string.Empty;
        facts["thing"] = lemma;
        var recipe = lemma.Length == 0 ? null : _recipes.ForOutput(lemma);
        if (recipe is null)
        {
            return Render(CraftNone, SlotMap.Empty, rng);
        }

        var need = Phrase(recipe.Ingredients.Select(static i => (i.Noun, i.Count)).ToList(), rng);
        facts["need"] = need;
        var lines = new List<string> { Render(CraftRecipe, SlotMap.Empty.With("thing", lemma).With("need", need), rng) };
        if (recipe.Station is { } station)
        {
            facts["station"] = station.Lemma;
            lines.Add(Render(CraftStationUse, SlotMap.Empty.With("station", station.Lemma), rng));
        }
        else
        {
            lines.Add(Render(CraftStationNone, SlotMap.Empty, rng));
        }

        if (scene.Inventory is { } inventory)
        {
            var missing = recipe.Ingredients
                .Where(i => inventory.Count(i.Item) < i.Count)
                .Select(i => (i.Noun, Count: i.Count - inventory.Count(i.Item)))
                .ToList();
            if (missing.Count == 0)
            {
                lines.Add(Render(CraftReady, SlotMap.Empty, rng));
            }
            else
            {
                var lack = Phrase(missing, rng);
                facts["lack"] = lack;
                var single = missing.Count == 1 && missing[0].Count == 1;
                lines.Add(Render(single ? CraftLackOne : CraftLackMany, SlotMap.Empty.With("lack", lack), rng));
            }
        }

        return string.Join(' ', lines);
    }

    // "seis bloques de madera", "dos bloques de madera y seis bloques de piedra": each part from the bundle's pools, joined with "y".
    private string Phrase(IReadOnlyList<(NounEntry Noun, int Count)> parts, SeededRng rng)
    {
        var rendered = new List<string>();
        foreach (var (noun, count) in parts)
        {
            var values = SlotMap.Empty
                .With("count", count.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .With("noun", noun.Lemma)
                .With("unit", BlockUnit);
            rendered.Add(Render(noun.Mass ? CraftPartMass : CraftPartCount, values, rng));
        }

        return rendered.Count == 1
            ? rendered[0]
            : string.Join(", ", rendered.Take(rendered.Count - 1)) + " y " + rendered[^1];
    }

    private (string Line, string? Noun) Identify(GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var noun = scene.Cursor is { } cursor ? NounAt(cursor) : null;
        if (noun is null)
        {
            return (Render(IdentifyNone, SlotMap.Empty, rng), null);
        }

        facts["target"] = noun;
        return (Render(IdentifyFound, SlotMap.Empty.With("target", noun), rng), noun);
    }

    // The guide's check of a name: the cursor's tile is the expected noun. A name is right when it is that noun and no error was found on it;
    // the core cannot grade against a host value (CR-014), so the game does. The word counts use the check: a name that is not the tile's noun
    // earns no colour (D-game-6 as amended).
    private (string Line, NameCheck Check, string? Target) Name(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        // A thing may have several nouns (the water is agua and río): the name is right when it is any of them; the guide says the first.
        var names = scene.Cursor is { } cursor ? _facts.NounsAt(cursor) : [];
        var target = names.FirstOrDefault();
        if (target is null)
        {
            return (Render(NameNone, SlotMap.Empty, rng), NameCheck.NoTarget, null);
        }

        facts["target"] = target;
        var named = verdict.Slots.TryGetValue("object", out var lemma) ? lemma : string.Empty;
        var right = names.Contains(named, StringComparer.Ordinal) && verdict.Errors.Count == 0;
        return (Render(right ? NameRight : NameWrong, SlotMap.Empty.With("target", target), rng), right ? NameCheck.Right : NameCheck.Wrong, target);
    }

    private static void Merge(Dictionary<string, string> facts, Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            facts[key] = value;
        }
    }

    private string? NounAt(GuideCell cell) => _facts.NounsAt(cell).FirstOrDefault();

    private string Render(string poolId, SlotMap slots, SeededRng rng)
    {
        // The game renders its own line from the bundle's pool with the bundle's template engine (see the type's remarks, CR-016). The draw is
        // always made, so the number of draws per turn does not depend on whether a pool has one variant or three.
        var pool = _bundle.Dialogue.TryGetPool(poolId, out var found)
            ? found
            : throw new InvalidOperationException($"The bundle has no dialogue pool '{poolId}'.");
        var variant = DialogueSelector.Select(pool, rng);
        return _bundle.TemplateEngine.Render(variant, slots);
    }
}
