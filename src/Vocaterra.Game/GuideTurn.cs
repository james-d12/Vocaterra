using System.Globalization;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

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
public sealed record GuidePointer(string Place, TilePoint Target, int Blocks, string Direction);

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
public sealed record GuideScene(TilePoint Player, TilePoint? Cursor = null, bool PointerActive = false, bool Review = false);

/// <summary>
/// The outcome of one typed turn with the guide: the core's verdict in plain values, the lines to show (the correction first, then the
/// guide's reply, then the game's own line with the world facts) and what the host events mean in the world. It has no time in it, so the same
/// inputs give an equal turn.
/// </summary>
/// <param name="Line">What the player typed, as sent.</param>
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
/// <param name="Naming">For a <c>name_object</c> turn, the guide's check of the name against the tile (the word counts withhold colour from a wrong name).</param>
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
    NameCheck Naming = NameCheck.None)
{
    /// <summary>A one-line summary of the verdict for the panel, for example <c>ask_directions, entendido, 100 %</c>.</summary>
    public string Summary => string.Create(
        CultureInfo.InvariantCulture,
        $"{Intent}, {(Communicated ? "entendido" : "no entendido")}, {(int)Math.Round(Correctness * 100, MidpointRounding.AwayFromZero)} %");

    /// <summary>Every line the guide says this turn, in the order shown: the correction, the replies, the repair, then the world line.</summary>
    public IReadOnlyList<string> GuideLines
    {
        get
        {
            var lines = new List<string>();
            if (Feedback is not null)
            {
                lines.Add(Feedback);
            }

            lines.AddRange(Replies);
            if (Repair is not null)
            {
                lines.Add(Repair);
            }

            if (WorldLine is not null)
            {
                lines.Add(WorldLine);
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

    /// <summary>The player named what they are looking at ("es un árbol"): the guide says whether the name fits the cursor's tile.</summary>
    public const string Name = "guide.name";
}
