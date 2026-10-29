
namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The closing moment of the session (S17, D-game-15): when the camp checklist is complete and the player's own fire is lit, a card says <c>Session complete</c> with
/// the guide's closing line in Spanish and in English, her voice says the line, and the card stays up for a few seconds (Esc or any key takes it down). Plain state
/// over the game's ticks; the card is drawn by <c>EndingLayer</c>, the voice and the console line come from the game when <see cref="Update"/> says the moment began.
/// </summary>
public sealed class SessionEnding
{
    /// <summary>How long the card stays up: forty-five seconds of game time (S17 showed it for twelve; the session summary of X04 needs time to read).</summary>
    public const long ShowTicks = 45 * 60;

    private long _since = -1;
    private bool _dismissed;

    /// <summary>Whether the moment has begun (the card was shown at least once).</summary>
    public bool Begun => _since >= 0;

    /// <summary>The Spanish closing line (the guide's, from the bundle), or empty before the moment.</summary>
    public string Spanish { get; private set; } = string.Empty;

    /// <summary>The English of the closing line.</summary>
    public string English { get; private set; } = string.Empty;

    /// <summary>The session summary's English lines (X04, R01), or none before the moment.</summary>
    public IReadOnlyList<string> Summary { get; private set; } = [];

    /// <summary>
    /// Checks the session once per tick. Returns <see langword="true"/> on the tick the moment begins (the session is complete for the first time), so the game can
    /// play the line and print it.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="tick">The game's tick.</param>
    public bool Update(VoxelSession session, long tick)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Begun || !session.SessionComplete)
        {
            return false;
        }

        _since = tick;
        Spanish = session.ClosingLine;
        English = session.ClosingLineId is { } closing && session.Locale.Line(closing) is { } gloss ? gloss : session.Locale.Ui("sessionCompleteLine");
        Summary = SessionSummary.Lines(SummaryFacts.Of(session), session.Locale);
        return true;
    }

    /// <summary>Whether the card is up at this tick.</summary>
    /// <param name="tick">The game's tick.</param>
    public bool Visible(long tick) => Begun && !_dismissed && tick - _since < ShowTicks;

    /// <summary>Takes the card down (a key was pressed).</summary>
    public void Dismiss() => _dismissed = true;

    /// <summary>Puts the moment back as it was when a save was loaded: a session that was complete then does not play its ending again.</summary>
    /// <param name="session">The 3D game.</param>
    public void MarkSeen(VoxelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.SessionComplete)
        {
            _since = 0;
            _dismissed = true;
            Spanish = session.ClosingLine;
        }
    }
}
