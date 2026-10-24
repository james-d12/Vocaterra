using Vocaterra.Game;
using Vocaterra.Game.Language.Words;
using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The word cards of the first pickups: one at a time, in order, each for its own number of ticks.</summary>
public sealed class WordCardQueueTests
{
    [Fact]
    public void CardsGoUpInOrderAndEachStaysForItsDuration()
    {
        var queue = new WordCardQueue();
        queue.Add(new WordCard("madera", "la madera"));
        queue.Add(new WordCard("fruta", "la fruta"));

        var first = queue.Current(0);
        Assert.Equal("la madera", first!.Card.Phrase);
        Assert.Equal(1, queue.Waiting);

        Assert.Equal("la madera", queue.Current(WordCardQueue.Duration - 1)!.Card.Phrase);
        var second = queue.Current(WordCardQueue.Duration);
        Assert.Equal("la fruta", second!.Card.Phrase);
        Assert.Equal(WordCardQueue.Duration, second.StartTick);

        Assert.Null(queue.Current(2 * WordCardQueue.Duration));
    }

    [Fact]
    public void AnEmptyQueueShowsNothing()
    {
        var queue = new WordCardQueue();

        Assert.Null(queue.Current(10));
        Assert.Equal(0, queue.Waiting);
    }
}
