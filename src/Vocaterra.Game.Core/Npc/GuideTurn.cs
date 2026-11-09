using System.Globalization;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Npc;

/// <summary>One mistake the core found in the typed line.</summary>
/// <param name="Tag">The error tag, for example <c>gender.agree</c>.</param>
/// <param name="Expected">The form the core expected.</param>
/// <param name="Actual">The form the player wrote.</param>
/// <param name="Span">Where in the normalized line the mistake is (the core's span).</param>
public sealed record GuideError(string Tag, string Expected, string Actual, TextSpan Span);

/// <summary>Where the guide points: the nearest tile of what the player asked for.</summary>
/// <param name="Place">The noun the player asked about, as a lemma (<c>árbol</c>).</param>
/// <param name="Target">The tile found.</param>
/// <param name="Blocks">The distance in blocks.</param>
/// <param name="Direction">The direction as a Spanish phrase.</param>
public sealed record GuidePointer(string Place, GuideCell Target, int Blocks, string Direction);

/// <summary>
/// One normalized word of the typed line with the reading the core's resolver chose for it (<c>PipelineResult.Words</c>, CR-002). The word
/// counts read the catalogue nouns from these, so they follow the verdict's own analysis; the learner's word record (S19) folds the
/// readings too.
/// </summary>
/// <param name="Text">The normalized token (its form: <c>aguas</c>; a number word is its digit, <c>2</c>).</param>
/// <param name="Span">Its span in the normalized line, the one the verdict's errors use.</param>
/// <param name="Lemma">The chosen reading's lemma (<c>agua</c>, the infinitive <c>querer</c>), or <see langword="null"/> for a name or an unknown word.</param>
/// <param name="Pos">The chosen reading's part of speech (<c>noun</c>, <c>verb</c>, ...), or <see langword="null"/>.</param>
/// <param name="Role">Whether the lexicon knows the word (<see cref="WordRole.Word"/>), a grammar binds it as a name, or it is unknown.</param>
/// <param name="Person">The reading's person (<c>1</c>), or <see langword="null"/> where it does not apply.</param>
/// <param name="Number">The reading's number (<c>singular</c>), or <see langword="null"/>.</param>
/// <param name="Tense">The reading's tense (<c>present</c>), or <see langword="null"/>.</param>
/// <param name="Mood">The reading's mood, or <see langword="null"/>.</param>
/// <param name="Gender">The reading's gender (for a noun), or <see langword="null"/>.</param>
public sealed record GuideWord(
    string Text,
    TextSpan Span,
    string? Lemma,
    string? Pos,
    WordRole Role = WordRole.Unknown,
    string? Person = null,
    string? Number = null,
    string? Tense = null,
    string? Mood = null,
    string? Gender = null)
{
    /// <summary>The words of a pipeline result, in order: every token with its role and the reading the resolver chose.</summary>
    /// <param name="result">The result of one evaluated line.</param>
    public static IReadOnlyList<GuideWord> From(PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var words = From(result.Words);
        if (result.Notes is not { Count: > 0 } notes)
        {
            return words;
        }

        // V26: a verb typed without the accent of its tense (M70) is counted as the form the core read, `compré`, not the written `compre`: the reading is the accented form's.
        return [.. words.Select(word => notes.FirstOrDefault(note => note.Span == word.Span) is { } note ? word.ReadAs(note) : word)];
    }

    /// <summary>The word as the core's accent note reads it (V30): the accented form with the note's reading (person, number, tense, mood), not the written word's own, so <c>compro</c> for the answer <c>compró</c> is the third person's.</summary>
    private GuideWord ReadAs(AccentNote note) => this with
    {
        Text = note.Accented,
        Lemma = note.Reading.Lemma,
        Pos = note.Reading.Pos,
        Person = note.Reading.Person,
        Number = note.Reading.Number,
        Tense = note.Reading.Tense,
        Mood = note.Reading.Mood,
        Gender = note.Reading.Gender,
    };

    /// <summary>The words of a phase-1 analysis (<c>UtteranceAnalysis.Words</c>), in order; empty for <see langword="null"/>.</summary>
    /// <param name="words">The resolved words of one analysed line.</param>
    public static IReadOnlyList<GuideWord> From(IReadOnlyList<ResolvedWord>? words) =>
        (words ?? []).Select(static w => new GuideWord(
            w.Token.Text,
            w.Token.Span,
            w.Reading?.Lemma,
            w.Reading?.Pos,
            w.Role,
            w.Reading?.Person,
            w.Reading?.Number,
            w.Reading?.Tense,
            w.Reading?.Mood,
            w.Reading?.Gender)).ToList();
}

/// <summary>The guide's check of a name the player gave an object (<c>name_object</c>) against the tile it is about.</summary>
public enum NameCheck
{
    /// <summary>The turn was not a naming, or was not understood.</summary>
    None,

    /// <summary>The noun is the tile's noun and the core found no error.</summary>
    Right,

    /// <summary>The noun is not the tile's noun (or the line had an error): the guide corrects it.</summary>
    Wrong,

    /// <summary>There was no tile to check against (the cursor was on nothing).</summary>
    NoTarget,
}

/// <summary>What the front-end tells the guide about the moment of a turn: where the player is, what the cursor is on and whether an arrow is out.</summary>
/// <param name="Player">The tile under the middle of the player's body (the origin of every direction the guide gives).</param>
/// <param name="Cursor">The tile under the mouse pointer, or <see langword="null"/> when there is none.</param>
/// <param name="PointerActive">Whether an arrow from an earlier question is still on screen (the help line then says to follow it).</param>
/// <param name="Review">Whether the line answers the guide's review question: it is evaluated on the <c>guide-review</c> prompt and <paramref name="Cursor"/> is the tile she pointed at.</param>
/// <param name="PromptOverride">The prompt to evaluate the line on instead of the NPC's own (S8: the guide's phrase review is answered on the prompt the phrase was taught at, so the core recognizes its intent); <see langword="null"/> for an ordinary turn.</param>
/// <param name="Facts">The world as the guide sees it for this turn: a copy of the blocks around the player taken when the line was submitted (<see cref="WorldFacts.Snapshot"/>), so the turn does not read a world that keeps changing under the worker thread; <see langword="null"/> uses the facts the guide was built with.</param>
/// <param name="Inventory">What the player carries, for the crafting and finding answers ("te faltan dos bloques de madera", "tienes un hacha"); <see langword="null"/> is read as nothing carried and nothing said about it.</param>
/// <param name="OrderHeld">Whether the NPC talked to has an order whose goods the player carries and that a lesson keeps back (V21): a purchase the coins do not cover is then answered <c>cant_pay_wait</c>.</param>
/// <param name="Credited">The phrases of the lessons the learner has begun or done when the line was submitted (village-plan V20, <c>LessonRunner.Credits</c>): a line in free talk that is
/// one of them is that phrase for the learner model, at any NPC; <see langword="null"/> for none.</param>
/// <param name="Offer">The host event of a teacher's offer to bake (W08, <c>baker.offer_craft</c>) when the teacher could make it now, or <see langword="null"/>: an off-prompt <c>ask_craft</c> is then answered by the offer instead of the NPC's one off-prompt line.</param>
/// <param name="Grant">What the teacher could give of the run's ingredients now (W08, <see cref="TeachingGrant"/>), or <see langword="null"/> with no run at a station.</param>
/// <param name="ClosedGifts">The gifts that are not to be given now (N06, <c>GiftRule.Key</c>: <c>shopkeeper:bread</c>), those given already today that no lesson step requires; <see langword="null"/> for none. An NPC asked for one answers <c>&lt;npc&gt;.gift_used</c>.</param>
public sealed record GuideScene(GuideViewpoint Player, GuideCell? Cursor = null, bool PointerActive = false, bool Review = false, Inventory? Inventory = null, IGuideFacts? Facts = null, string? PromptOverride = null, IReadOnlySet<string>? Credited = null, bool OrderHeld = false, IReadOnlySet<string>? ClosedGifts = null, TeachingGrant? Grant = null, string? Offer = null);

/// <summary>
/// The outcome of one typed turn with the guide: the core's verdict in plain values, the lines to show (the correction first, then the
/// guide's reply, then the game's own line with the world facts) and what the host events mean in the world. It has no time in it, so the same
/// inputs give an equal turn.
/// </summary>
/// <param name="Line">What the player typed, as sent; for a spoken line, the transcript the recognizer heard.</param>
/// <param name="Intent">The intent the core resolved (<c>ask_directions</c>, or <c>unknown</c>).</param>
/// <param name="Communicated">Whether the guide understood.</param>
/// <param name="Correctness">0 to 1, how well the line was written.</param>
/// <param name="Errors">The mistakes found, in the order the core reports them.</param>
/// <param name="Feedback">The correction line (a recast, a hint or an explicit correction), or <see langword="null"/> when the line was right.</param>
/// <param name="Replies">The guide's own lines for this turn (the agent's reply, or the not-understood line).</param>
/// <param name="Repair">The repair line when the guide did not understand, or <see langword="null"/>.</param>
/// <param name="AgentState">The guide's state after the turn.</param>
/// <param name="HostEvents">The host events the state machine raised (<c>guide.point</c>, ...), in order.</param>
/// <param name="WorldLine">The game's own line with the world facts, or <see langword="null"/>.</param>
/// <param name="Pointer">Set when the guide points at a tile (the point-at event with a target in range).</param>
/// <param name="IdentifiedNoun">Set when the guide named what the cursor is on (<c>árbol</c>).</param>
/// <param name="HostStateHash">The host state hash the core copied into this turn's replay record: the simulation's state digest when the line was sent.</param>
/// <param name="Slots">The slot values the core filled from the line (<c>place</c>, <c>object</c>), for the word counts.</param>
/// <param name="Naming">For a <c>name_object</c> turn, the guide's check of the name against the tile (the word counts withhold colour from a wrong name and from a name with no tile to check).</param>
/// <param name="NameTarget">For a <see cref="NameCheck.Wrong"/> or <see cref="NameCheck.Right"/> check, the noun of the tile (the word the guide revealed when the name was wrong).</param>
/// <param name="Words">The line's normalized words with the resolver's readings (<see cref="GuideWord.From(PipelineResult)"/>), or <see langword="null"/> for a turn built by hand.</param>
/// <param name="Input">How the line arrived (typed, spoken, or sent by a button); the word counts apply the credit of this kind (<see cref="CreditRules"/>).</param>
/// <param name="Confidence">For a spoken line, how sure Vosk was of the words (0 to 1, the mean of its per-word confidences without the <c>[unk]</c> marks, <see cref="UncertaintyRule.MeanWordConfidence"/>); <see langword="null"/> for a typed line or a button.</param>
/// <param name="Npc">The NPC the line was said to (<c>guide</c>, <c>shopkeeper</c>, <c>villager</c>); the type's name is the guide's from S3, when she was the only one.</param>
/// <param name="Prompt">The prompt the line was evaluated on (<c>stall-need</c>).</param>
/// <param name="RepairKind">The core's repair step after an unclear line (ask again, slow repeat, offer choices, give up), or <see langword="null"/> for a line that was understood.</param>
/// <param name="FollowUp">What the NPC says after the rest because of a host decision, not the player's line (the shopkeeper's fail-forward line), or <see langword="null"/>.</param>
/// <param name="FailForward">Whether the game carried on without the player's phrase: the shopkeeper handed over the bottle after the exchange ran out (<c>stall.give_water</c> is among <paramref name="HostEvents"/>).</param>
/// <param name="Choices">When the repair ladder offered choices (<see cref="RepairKind.OfferChoices"/>), the two or three phrases it offered, in order; otherwise <see langword="null"/>. The panel shows them as buttons (<see cref="PhraseSheet.OptionsFor"/>).</param>
/// <param name="Uncertain">Whether a spoken line was an uncertain recognition (D-game-9, <see cref="UncertaintyRule"/>): the NPC did not understand it, or the recognizer was not sure of its words. Never a language failure.</param>
/// <param name="Choice">Whether the line was picked among the offered choices (a recognition signal, never counted as production; <see cref="Input"/> is then <see cref="InputKind.Button"/>).</param>
public sealed record GuideTurn(
    string Line,
    string Intent,
    bool Communicated,
    double Correctness,
    IReadOnlyList<GuideError> Errors,
    string? Feedback,
    IReadOnlyList<string> Replies,
    string? Repair,
    string AgentState,
    IReadOnlyList<string> HostEvents,
    string? WorldLine,
    GuidePointer? Pointer,
    string? IdentifiedNoun,
    string HostStateHash,
    SlotMap Slots,
    NameCheck Naming = NameCheck.None,
    string? NameTarget = null,
    IReadOnlyList<GuideWord>? Words = null,
    InputKind Input = InputKind.Typed,
    double? Confidence = null,
    string Npc = NpcService.GuideAgent,
    string Prompt = "",
    RepairKind? RepairKind = null,
    IReadOnlyList<string>? FollowUp = null,
    bool FailForward = false,
    IReadOnlyList<string>? Choices = null,
    bool Uncertain = false,
    bool Choice = false)
{
    /// <summary>
    /// Whether a lesson step took this turn (S17): the learner answered what the step asked, so the lesson speaks for the guide and her ordinary reply and
    /// world line (<c>Replies</c>, <c>WorldLine</c>) are not shown or played. The correction and any follow-up stay. Set by the session when it applies the turn.
    /// </summary>
    public bool LessonHandled { get; init; }

    /// <summary>
    /// The core's notes on verbs typed without the accent of their tense (M70, V21b): <c>«compré», con tilde: es pasado</c>, one per word, worded by the Spanish pack
    /// (<c>SpanishAccentNotes.Describe</c>). The line was accepted with no deduction; the notes only tell the learner the accent. Empty when there is none.
    /// </summary>
    public IReadOnlyList<string> AccentNotes { get; init; } = [];

    /// <summary>
    /// The intent the core found for a line the prompt does not take (<c>Verdict.OffPromptIntent</c>, M72), set when the NPC answered it with its off-prompt line in free
    /// talk (V27); <see langword="null"/> otherwise. The line is not <see cref="Communicated"/>, and no repair step follows it.
    /// </summary>
    public string? OffPromptIntent { get; init; }

    /// <summary>
    /// Whether a spoken line was read from the core's second, open decode (V31, M80): the constrained decode at a free-talk prompt did not take it, the free decode did.
    /// The recognizer may have smoothed a mistake away, so the line is credited as a typed one is (<see cref="CreditKind"/>); the session log and a voice event mark it.
    /// </summary>
    public bool HeardOpen { get; init; }

    /// <summary>The kind of input the credit of this turn is counted as (V31, the owner's answer 3): the input itself, except a line heard on the open decode, which counts as typed (half).</summary>
    public InputKind CreditKind => HeardOpen ? InputKind.Typed : Input;

    /// <summary>Whether the NPC took the line: the core communicated it, or it is one the NPC understood off its prompt (V27). The book credits what it takes.</summary>
    public bool Taken => Communicated || OffPromptIntent is not null;

    /// <summary>
    /// The session's plain-English notes after this turn (issue #42: what to say to hand an order over, or that the NPC took it anyway), shown under the turn's feedback.
    /// Set by the session when it applies the turn; empty when there is none.
    /// </summary>
    public IReadOnlyList<string> EnglishNotes { get; init; } = [];

    /// <summary>Whether the follow-up lines are the guide's last line said again slowly (the learner asked <c>Más despacio, por favor</c>, S17): the front-end plays the slow recording.</summary>
    public bool SlowFollowUp { get; init; }

    /// <summary>
    /// Whether the line was held back as an unsure recognition (<see cref="UncertaintyRule.IsTooUnsure"/>): the NPC would have understood it but the
    /// recognizer was not sure of the words, so the turn was not completed and nothing changed in the conversation, the learner or the world.
    /// </summary>
    public bool Unsure => Uncertain && RepairKind is null;

    /// <summary>A one-line summary of the verdict for the panel, for example <c>ask_directions, entendido, 100 %</c>.</summary>
    public string Summary => string.Create(
        CultureInfo.InvariantCulture,
        $"{Intent}, {(Communicated ? "understood" : "not understood")}, {(int)Math.Round(Correctness * 100, MidpointRounding.AwayFromZero)} %");

    /// <summary>Every line the NPC says this turn, in the order shown: the correction, the replies, the repair, the world line, then the follow-up.</summary>
    public IReadOnlyList<string> GuideLines
    {
        get
        {
            var lines = new List<string>();
            if (Feedback is not null)
            {
                lines.Add(Feedback);
            }

            if (!LessonHandled)
            {
                lines.AddRange(Replies);
            }

            if (Repair is not null)
            {
                lines.Add(Repair);
            }

            if (WorldLine is not null && !LessonHandled)
            {
                lines.Add(WorldLine);
            }

            if (FollowUp is not null)
            {
                lines.AddRange(FollowUp);
            }

            return lines;
        }
    }
}

/// <summary>The host event names the guide's state machine raises (the bundle's <c>agents/guide.json</c>); the core treats them as opaque strings.</summary>
public static class GuideEvents
{
    /// <summary>The player asked where something is: point at it.</summary>
    public const string Point = "guide.point";

    /// <summary>The player asked what the cursor is on: name it.</summary>
    public const string Identify = "guide.identify";

    /// <summary>The player asked for help: give a hint.</summary>
    public const string Help = "guide.help";

    /// <summary>The player greeted the guide.</summary>
    public const string Greet = "agent.greet";

    /// <summary>The player said goodbye.</summary>
    public const string Farewell = "agent.farewell";

    /// <summary>The player asked how to make something: the recipe, the station and what is missing.</summary>
    public const string Craft = "guide.craft";

    /// <summary>The player asked where a material is or comes from: its source and the nearest one.</summary>
    public const string Find = "guide.find";

    /// <summary>The player named what they are looking at ("es un árbol"): the guide says whether the name fits the cursor's tile.</summary>
    public const string Name = "guide.name";

    /// <summary>The player asked the guide to say her last line again ("Otra vez, por favor", S17): the line is shown and played again.</summary>
    public const string Repeat = "guide.repeat";

    /// <summary>The player asked the guide to say her last line again more slowly ("Más despacio, por favor", S17): the line is shown again and the slow recording plays.</summary>
    public const string RepeatSlow = "guide.repeat_slow";
}

/// <summary>
/// What a teacher could give of a run's ingredients at the moment of a turn (W08, design decision 20), as plain values the worker can read: the ingredients the bag lacks, whether
/// the grant is open (the first bake always, otherwise once per in-game day) and whether the bag has room for them.
/// </summary>
/// <param name="Missing">The ingredients the run uses and the bag lacks, with the counts that are lacking (empty when nothing is missing).</param>
/// <param name="Open">Whether the grant may be made now.</param>
/// <param name="Fits">Whether every missing ingredient fits in the bag.</param>
public sealed record TeachingGrant(IReadOnlyList<(ItemId Item, int Count)> Missing, bool Open, bool Fits)
{
    /// <summary>The host event the teacher answers when nothing can be given, or <see langword="null"/> when the grant can be made.</summary>
    public string? Miss => Missing.Count == 0 ? NpcHostEvents.NothingMissing : !Open ? NpcHostEvents.GiftUsed : !Fits ? NpcHostEvents.NoRoom : null;
}
