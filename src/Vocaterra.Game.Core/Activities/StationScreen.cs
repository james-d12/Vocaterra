using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Activities;

/// <summary>
/// The station overlay's state (W07; the pattern of <see cref="Items.Crafting.CraftingScreen"/>): open or closed, the page, what is selected to hold and to act on, the verb choice
/// and what the last act was. <see cref="View"/> turns it, with the session's run and the bag, into the plain values the layer draws. Opening it and every change of what is
/// shown counts each shown noun as read once per bake (<see cref="NounsToRead"/>): seeing a label is a read, never a spoken credit (design 10.10).
/// </summary>
public sealed class StationScreen
{
    /// <summary>The cells of one page of what can be held; the keys 1 to 9 select them.</summary>
    public const int PageSize = 9;

    /// <summary>The noun of the empty hand (key 0).</summary>
    public const string HandNoun = "mano";

    /// <summary>The icon key of the empty hand.</summary>
    public const string HandIcon = "hand";

    private readonly HashSet<string> _read = new(StringComparer.Ordinal);
    private int _bake = -1;

    /// <summary>Whether the overlay is shown. While it is, the player does not walk or look, and the needs stand still.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The station being worked, or an empty text when closed.</summary>
    public string StationId { get; private set; } = string.Empty;

    /// <summary>The procedure being done, or an empty text when closed.</summary>
    public string ProcedureId { get; private set; } = string.Empty;

    /// <summary>The page of what can be held, from 0.</summary>
    public int Page { get; internal set; }

    /// <summary>The noun selected to hold, or <see langword="null"/>.</summary>
    public string? Held { get; internal set; }

    /// <summary>The noun selected to act on, or <see langword="null"/> (the first target is taken then).</summary>
    public string? Target { get; internal set; }

    /// <summary>The open verb choice, or <see langword="null"/>.</summary>
    public VerbChoice? Verbs { get; internal set; }

    /// <summary>What the last act was.</summary>
    public StationNotice Notice { get; internal set; }

    /// <summary>How many of the item the finished bake gave.</summary>
    public int Made { get; internal set; }

    /// <summary>The nouns of the ingredients gone at the end of the bake.</summary>
    public IReadOnlyList<string> Missing { get; internal set; } = [];

    /// <summary>The way to bake the learner picked (a pick of a held thing or a target is the guided one), or <see langword="null"/> for the default.</summary>
    internal BakeChoice? Picked { get; set; }

    /// <summary>The row whose audio was last started (a row is spoken once when it appears), or <see langword="null"/>.</summary>
    internal string? SpokenRow { get; set; }

    /// <summary>Whether the audio of the row now taught was heard at least once (decision 19: the act's evidence).</summary>
    internal bool HeardRow { get; set; }

    /// <summary>Opens the overlay on a station and a procedure; the labels shown count as read once in each bake.</summary>
    /// <param name="stationId">The station.</param>
    /// <param name="procedureId">The procedure.</param>
    /// <param name="bake">The number of the bake in play (the procedure's count of runs): a label already counted in it is not counted again when the overlay is opened once more.</param>
    public void Open(string stationId, string procedureId, int bake)
    {
        ArgumentNullException.ThrowIfNull(stationId);
        ArgumentNullException.ThrowIfNull(procedureId);
        StationId = stationId;
        ProcedureId = procedureId;
        IsOpen = true;
        NewBake(bake);
    }

    /// <summary>Closes the overlay (the run stays where it is).</summary>
    public void Close()
    {
        IsOpen = false;
        Verbs = null;
    }

    /// <summary>The selections, the notice and the spoken row start again; the read labels start again only when the bake is another one.</summary>
    /// <param name="bake">The number of the bake in play.</param>
    internal void NewBake(int bake)
    {
        if (bake != _bake)
        {
            _bake = bake;
            _read.Clear();
        }

        Page = 0;
        Held = null;
        Target = null;
        Verbs = null;
        Notice = StationNotice.None;
        Made = 0;
        Missing = [];
        SpokenRow = null;
        HeardRow = false;
        Picked = null;
    }

    /// <summary>The nouns of a view not yet counted as read in this bake; they are counted by this call.</summary>
    /// <param name="view">The view just shown.</param>
    public IReadOnlyList<string> NounsToRead(StationView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var fresh = new List<string>();
        foreach (var cell in view.Fixtures.Concat(view.Targets).Concat(view.Held).Append(view.Hand))
        {
            if (_read.Add(cell.Noun))
            {
                fresh.Add(cell.Noun);
            }
        }

        return fresh;
    }

    /// <summary>
    /// Everything that can be held, in the order of its pages: the bag's items (one entry per noun, the first stack's label), the station's tools, and the things the steps have
    /// made and not yet used. The empty hand is not in the list (key 0).
    /// </summary>
    /// <param name="session">The session whose run, nouns and procedures are read.</param>
    /// <param name="bag">The learner's bag.</param>
    public static IReadOnlyList<HeldThing> HeldThings(GuideSession session, Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(bag);
        var things = new List<HeldThing>();
        foreach (var stack in bag.Slots.Where(static s => !s.IsEmpty))
        {
            if (session.Nouns.ForItem(stack.Item) is not { } noun)
            {
                continue; // an item with no Spanish noun cannot be named in an act
            }

            var at = things.FindIndex(t => string.Equals(t.Noun, noun.Lemma, StringComparison.Ordinal));
            if (at >= 0)
            {
                things[at] = things[at] with { Count = things[at].Count + stack.Count };
            }
            else
            {
                things.Add(new HeldThing(StationCellKind.Bag, noun.Lemma, session.Data.Items.Get(stack.Item).Name, stack.Item, stack.Count));
            }
        }

        if (Station(session) is not { } station || Procedure(session) is not { } procedure)
        {
            return things;
        }

        foreach (var fixture in station.Fixtures.Where(f => procedure.Steps.Any(s => string.Equals(s.ActUse, f.Noun, StringComparison.Ordinal))))
        {
            things.Add(new HeldThing(StationCellKind.Tool, fixture.Noun, fixture.Icon, null, 0));
        }

        foreach (var noun in Intermediates(session, station, procedure))
        {
            things.Add(new HeldThing(StationCellKind.Intermediate, noun, noun, null, 0));
        }

        return things;
    }

    /// <summary>
    /// The things the steps make: a noun some step acts with that is not a fixture, not the hand and not a bag item's noun. It exists once the last step that acts with a tool before
    /// its first use is done (the mixing makes the dough), or, with no such step, once every step before its first use is done; it is gone once its last use is done.
    /// </summary>
    private static List<string> Intermediates(GuideSession session, StationDefinition station, ProcedureDefinition procedure)
    {
        var order = session.Procedures.Order.Select(id => procedure.Steps.First(s => string.Equals(s.Id, id, StringComparison.Ordinal))).ToList();
        var done = session.Procedures.View().StepsDone;
        var made = new List<string>();
        foreach (var noun in order.Select(static s => s.ActUse).Distinct(StringComparer.Ordinal))
        {
            if (station.HasFixture(noun) || string.Equals(noun, HandNoun, StringComparison.Ordinal) || session.Nouns.ForLemma(noun)?.Items.Count > 0)
            {
                continue;
            }

            var first = order.FindIndex(s => string.Equals(s.ActUse, noun, StringComparison.Ordinal));
            var last = order.FindLastIndex(s => string.Equals(s.ActUse, noun, StringComparison.Ordinal));
            var producer = order.Take(first).ToList().FindLastIndex(static s => s.With is not null);
            if (producer < 0)
            {
                producer = first - 1;
            }

            if (done > producer && done <= last)
            {
                made.Add(noun);
            }
        }

        return made;
    }

    /// <summary>The station definition being worked, or <see langword="null"/>.</summary>
    private static StationDefinition? Station(GuideSession session) => session.Procedures.Current is { } p ? session.Data.Stations.Find(p.Station) : null;

    private static ProcedureDefinition? Procedure(GuideSession session) => session.Procedures.Current;

    /// <summary>
    /// What the overlay shows now. A selection that no longer exists (the item was used up, the made thing was used) is dropped, and the first target is taken when none is chosen.
    /// </summary>
    /// <param name="session">The session whose run is shown.</param>
    /// <param name="bag">The learner's bag.</param>
    /// <param name="audio">Whether the row can be heard.</param>
    public StationView View(GuideSession session, Inventory bag, IInstructionAudio audio)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(audio);
        var procedure = Procedure(session);
        var station = Station(session);
        if (procedure is null || station is null)
        {
            return new StationView(StationId, TitleOf(session, StationId), ProcedureId, ProcedureState.NotStarted, InstructionStrip.Empty, [], [], [], Cell(session, StationCellKind.Hand, HandNoun, HandIcon, null, 0, false, false, 0), 0, 1, null, null, null, StationNotice.None, 0, [], null, null);
        }

        var run = session.Procedures.View();
        var strip = InstructionStrip.From(run, run.Row is null ? null : session.Phrases.Find(run.Row), session.Data.Words.Buttons, run.Row is not null && audio.CanPlay(run.Row));
        var all = HeldThings(session, bag);
        if (Held is not null && !string.Equals(Held, HandNoun, StringComparison.Ordinal) && !all.Any(t => string.Equals(t.Noun, Held, StringComparison.Ordinal)))
        {
            Held = null;
        }

        var targetNouns = TargetNouns(station, procedure);
        if (Target is null || !targetNouns.Contains(Target, StringComparer.Ordinal))
        {
            Target = targetNouns.Count > 0 ? targetNouns[0] : null;
        }

        var pageCount = Math.Max(1, (all.Count + PageSize - 1) / PageSize);
        Page = Math.Clamp(Page, 0, pageCount - 1);
        bool Lit(string noun, string? lit) => lit is not null && string.Equals(noun, lit, StringComparison.Ordinal);
        var held = all.Skip(Page * PageSize).Take(PageSize).Select((t, i) =>
            Cell(session, t.Kind, t.Noun, t.Icon, t.Item, t.Count, Lit(t.Noun, run.HighlightUse), string.Equals(t.Noun, Held, StringComparison.Ordinal), i + 1)).ToList();
        var hand = Cell(session, StationCellKind.Hand, HandNoun, HandIcon, null, 0, Lit(HandNoun, run.HighlightUse), string.Equals(Held, HandNoun, StringComparison.Ordinal), 0);
        var fixtures = station.Fixtures.Select(f => Cell(session, targetNouns.Contains(f.Noun, StringComparer.Ordinal) ? StationCellKind.Target : StationCellKind.Tool, f.Noun, f.Icon, null, 0, false, false, null)).ToList();
        var targets = station.Fixtures.Where(f => targetNouns.Contains(f.Noun, StringComparer.Ordinal))
            .Select(f => Cell(session, StationCellKind.Target, f.Noun, f.Icon, null, 0, Lit(f.Noun, run.HighlightOn), string.Equals(f.Noun, Target, StringComparison.Ordinal), null)).ToList();
        return new StationView(
            station.Id,
            TitleOf(session, station.Id),
            procedure.Id,
            run.State,
            strip,
            fixtures,
            targets,
            held,
            hand,
            Page,
            pageCount,
            Held is null ? null : LabelOf(session, Held, all),
            Target is null ? null : session.Nouns.ForLemma(Target)?.WithDefinite ?? Target,
            Verbs,
            Notice,
            Made,
            Missing,
            Made > 0 && session.Data.Items.Find(procedure.Makes.Item) is { } made ? session.Nouns.ForItem(made.Id)?.WithDefinite : null,
            OfferOf(session, procedure, run.State));
    }

    // The one-click bake of a mastered procedure (design 10.15): offered while the run is not waiting to be collected; the guided bake comes first, and is selected by default,
    // when the goal row is due again.
    private BakeOffer? OfferOf(GuideSession session, ProcedureDefinition procedure, ProcedureState state)
    {
        if (state == ProcedureState.ReadyToCollect || !session.Procedures.Progress.IsMastered(procedure.Id) || ProcedureRunner.Items(procedure, session.Data.Items) is null)
        {
            return null;
        }

        var made = session.Data.Items.Find(procedure.Makes.Item);
        var label = made is null ? procedure.Makes.Item : session.Nouns.ForItem(made.Id)?.WithDefinite ?? procedure.Makes.Item;
        var guidedFirst = session.PhraseProgress.IsScaffoldDue(procedure.Goal.Row, session.StoredTime.UnixSeconds, session.Credit);
        return new BakeOffer(procedure.Id, label, guidedFirst, Picked == BakeChoice.OneClick || (Picked is null && !guidedFirst));
    }

    // The fixtures the steps act on; a fixture no step names at all is a target too (it may be shown and chosen, and a wrong act on it is a wrong act).
    private static List<string> TargetNouns(StationDefinition station, ProcedureDefinition procedure) =>
        station.Fixtures.Where(f => procedure.Steps.Any(s => string.Equals(s.ActOn, f.Noun, StringComparison.Ordinal))
            || !procedure.Steps.Any(s => string.Equals(s.ActUse, f.Noun, StringComparison.Ordinal))).Select(static f => f.Noun).ToList();

    /// <summary>The station's name for the panel's title (design 10.16): its locale label when the locale has one, else its noun with the article when its id is a catalogue noun, else its id.</summary>
    /// <param name="session">The session (its locale and nouns).</param>
    /// <param name="stationId">The station's id.</param>
    internal static string TitleOf(GuideSession session, string stationId) =>
        session.Locale.Find("ui.station." + stationId) ?? session.Nouns.ForLemma(stationId)?.WithDefinite ?? stationId;

    private static string LabelOf(GuideSession session, string noun, IReadOnlyList<HeldThing> all) =>
        Label(session, noun, string.Equals(noun, HandNoun, StringComparison.Ordinal) ? 0 : all.FirstOrDefault(t => string.Equals(t.Noun, noun, StringComparison.Ordinal))?.Count ?? 0);

    private static string Label(GuideSession session, string noun, int count)
    {
        var text = session.Nouns.ForLemma(noun)?.WithDefinite ?? noun;
        return count > 0 ? $"{text} ×{count.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : text;
    }

    private static StationCell Cell(GuideSession session, StationCellKind kind, string noun, string icon, ItemId? item, int count, bool highlight, bool selected, int? key) =>
        new(kind, noun, Label(session, noun, count), icon, item, count, session.Words.RecordOf(noun).Known ? null : session.Locale.NounSupport(noun), highlight, selected, key);
}

/// <summary>One thing the learner can hold at the station (before it becomes a cell of a page).</summary>
/// <param name="Kind">Bag item, tool or made thing.</param>
/// <param name="Noun">The catalogue noun.</param>
/// <param name="Icon">The icon key.</param>
/// <param name="Item">The bag item, or <see langword="null"/>.</param>
/// <param name="Count">How many the bag holds.</param>
public sealed record HeldThing(StationCellKind Kind, string Noun, string Icon, ItemId? Item, int Count);
