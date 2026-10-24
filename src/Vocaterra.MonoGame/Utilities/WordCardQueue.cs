using Vocaterra.Game;
using Vocaterra.Game.Language.Words;

namespace Vocaterra.MonoGame.Utilities;

/// <summary>The card on screen and the tick it went up.</summary>
/// <param name="Card">The word card (<c>la madera</c>).</param>
/// <param name="StartTick">The simulation tick the card went up at.</param>
public sealed record ShownCard(WordCard Card, long StartTick);

/// <summary>
/// The word cards waiting to be shown, one at a time, each for <see cref="Duration"/> ticks of the simulation (no clock). A card that arrives
/// while another is up waits its turn.
/// </summary>
public sealed class WordCardQueue
{
    /// <summary>How long a card stays up, in ticks (five seconds).</summary>
    public const int Duration = 300;

    private readonly Queue<WordCard> _waiting = new();
    private WordCard? _current;
    private long _startTick;

    /// <summary>The cards waiting behind the one on screen.</summary>
    public int Waiting => _waiting.Count;

    /// <summary>Queues a card.</summary>
    public void Add(WordCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _waiting.Enqueue(card);
    }

    /// <summary>The card on screen at <paramref name="tick"/>, or <see langword="null"/>; the next waiting card goes up when the last one is down.</summary>
    public ShownCard? Current(long tick)
    {
        if (_current is not null && tick - _startTick >= Duration)
        {
            _current = null;
        }

        if (_current is null && _waiting.Count > 0)
        {
            _current = _waiting.Dequeue();
            _startTick = tick;
        }

        return _current is null ? null : new ShownCard(_current, _startTick);
    }
}
