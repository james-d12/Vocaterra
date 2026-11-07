using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Activities;

/// <summary>What a cell of the station is.</summary>
public enum StationCellKind
{
    /// <summary>The empty hand (key 0).</summary>
    Hand,

    /// <summary>An item of the bag.</summary>
    Bag,

    /// <summary>A fixture the learner picks up and acts with (the spoon).</summary>
    Tool,

    /// <summary>A thing the steps made (the dough), held once it exists.</summary>
    Intermediate,

    /// <summary>A fixture the learner acts on (the bowl, the oven).</summary>
    Target,
}

/// <summary>
/// One cell of the station view: a picture, a noun with its article and, for a bag item, its count (<c>la harina ×1</c>).
/// </summary>
/// <param name="Kind">What the cell is.</param>
/// <param name="Noun">The catalogue noun the act names (<c>harina</c>).</param>
/// <param name="Label">The noun with its article, and <c>×count</c> for a bag item.</param>
/// <param name="IconKey">The key the icon is found by (a fixture's icon from <c>stations.json</c>, a bag item's name, the noun for a made thing); a key with no art draws a plain tile.</param>
/// <param name="Item">The bag item that the icon draws, or <see langword="null"/>.</param>
/// <param name="Count">How many the bag holds (0 for anything else).</param>
/// <param name="Hover">The noun's English, shown on hover until the noun is known (D-game-6), or <see langword="null"/> when it is known or the locale has none.</param>
/// <param name="Highlight">Whether the cell is highlighted: the thing to hold or its target, after a slip or on help (design 10.10).</param>
/// <param name="Selected">Whether the cell is the learner's selection.</param>
/// <param name="Key">The digit that selects it (1 to 9 on the page, 0 for the hand), or <see langword="null"/>.</param>
public sealed record StationCell(
    StationCellKind Kind,
    string Noun,
    string Label,
    string IconKey,
    ItemId? Item,
    int Count,
    string? Hover,
    bool Highlight,
    bool Selected,
    int? Key);

/// <summary>The short choice of verbs of a pair that has several (design 10.7: <c>sacar</c> or <c>abrir</c>).</summary>
/// <param name="Use">What is held.</param>
/// <param name="On">The target.</param>
/// <param name="Verbs">The verbs, in the table's order.</param>
/// <param name="Cursor">The verb the Enter key would pick.</param>
public sealed record VerbChoice(string Use, string On, IReadOnlyList<string> Verbs, int Cursor);

/// <summary>What the station says after the last act.</summary>
public enum StationNotice
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>A right act.</summary>
    Right,

    /// <summary>Another thing or another target than the step's.</summary>
    WrongThing,

    /// <summary>The right thing and target with another verb.</summary>
    WrongVerb,

    /// <summary>A later step's act.</summary>
    OutOfOrder,

    /// <summary>The bake is done and the exchange made.</summary>
    Done,

    /// <summary>The bag has no room: nothing was used, Enter tries again.</summary>
    BagFull,

    /// <summary>Something the bake used is gone from the bag: its step is open again.</summary>
    Missing,

    /// <summary>The one-click bake lacks an ingredient: nothing was used.</summary>
    NotEnough,
}

/// <summary>Which of the two ways to bake is chosen: the guided one (the steps) or the one-click bake (design 10.15).</summary>
public enum BakeChoice
{
    /// <summary>The guided bake: the steps, as ever.</summary>
    Guided,

    /// <summary>The one-click bake of a mastered procedure.</summary>
    OneClick,
}

/// <summary>
/// The one-click bake of a mastered procedure, offered beside the guided bake (design 10.15): the two choices stand in the order given by <paramref name="GuidedFirst"/>, and one is selected.
/// </summary>
/// <param name="ProcedureId">The mastered procedure.</param>
/// <param name="Label">What it makes, with its article (<c>el pan</c>).</param>
/// <param name="GuidedFirst">Whether the guided bake comes first (the goal row is due again); otherwise the one-click bake does.</param>
/// <param name="OneClickSelected">Whether the one-click bake is selected: by default when the guided bake is not first, else only once picked.</param>
public sealed record BakeOffer(string ProcedureId, string Label, bool GuidedFirst, bool OneClickSelected);

/// <summary>
/// What the station overlay shows, as plain values (W07; the layer draws it, the layout places it, a test reads it): the instruction strip, the fixtures on the left, the
/// targets in the middle, the page of what can be held along the bottom with the empty hand, the selection and the verb choice.
/// </summary>
/// <param name="StationId">The station.</param>
/// <param name="Title">The station's name for the panel's title: its locale label (<c>ui.station.&lt;id&gt;</c>), else its noun with the article, else its id.</param>
/// <param name="ProcedureId">The procedure being done.</param>
/// <param name="State">The run's state.</param>
/// <param name="Strip">The instruction strip.</param>
/// <param name="Fixtures">Every fixture of the station, in the file's order (the left column).</param>
/// <param name="Targets">The fixtures the steps act on (the middle).</param>
/// <param name="Held">The current page of what can be held (at most <see cref="StationScreen.PageSize"/> cells).</param>
/// <param name="Hand">The empty hand (key 0), always shown.</param>
/// <param name="Page">The current page, from 0.</param>
/// <param name="PageCount">The pages (at least 1).</param>
/// <param name="HeldLabel">The held thing selected, even when it is on another page, or <see langword="null"/>.</param>
/// <param name="TargetLabel">The target selected, or <see langword="null"/>.</param>
/// <param name="Verbs">The verb choice, or <see langword="null"/> when none is open.</param>
/// <param name="Notice">What the last act was.</param>
/// <param name="Made">How many of the item the finished bake gave (0 before).</param>
/// <param name="Missing">The nouns of the ingredients gone at the end of the bake.</param>
/// <param name="MadeLabel">The label of the item the finished bake gave (<c>el pan</c>), or <see langword="null"/> before.</param>
/// <param name="Offer">The one-click bake's offer, or <see langword="null"/> before the procedure is mastered.</param>
/// <param name="Talk">What the learner's talk to the teacher shows (W08), or <see langword="null"/> when nobody listens or nothing is to be shown.</param>
public sealed record StationView(
    string StationId,
    string Title,
    string ProcedureId,
    ProcedureState State,
    InstructionStrip Strip,
    IReadOnlyList<StationCell> Fixtures,
    IReadOnlyList<StationCell> Targets,
    IReadOnlyList<StationCell> Held,
    StationCell Hand,
    int Page,
    int PageCount,
    string? HeldLabel,
    string? TargetLabel,
    VerbChoice? Verbs,
    StationNotice Notice,
    int Made,
    IReadOnlyList<string> Missing,
    string? MadeLabel = null,
    BakeOffer? Offer = null,
    StationTalkView? Talk = null);

/// <summary>What the line of the station's talk to the teacher shows (W08): the learner's text box, the recording, the thinking, or the teacher's answer.</summary>
/// <param name="Teacher">The teacher's name (<c>Luz</c>).</param>
/// <param name="State">What the line is.</param>
/// <param name="Text">The text box's contents, the learner's heard line, or the teacher's answer, by state; empty for the others.</param>
public sealed record StationTalkView(string Teacher, StationTalkState State, string Text);

/// <summary>What the talk line of the station is showing now (W08).</summary>
public enum StationTalkState
{
    /// <summary>The learner is typing a line: <see cref="StationTalkView.Text"/> is the text box.</summary>
    Typing,

    /// <summary>The microphone is recording.</summary>
    Listening,

    /// <summary>A turn is on the worker: the teacher is thinking.</summary>
    Thinking,

    /// <summary>The teacher answered: <see cref="StationTalkView.Text"/> is her line.</summary>
    Answer,

    /// <summary>The talk could not start (no microphone, no speech model): <see cref="StationTalkView.Text"/> says why.</summary>
    Problem,
}
