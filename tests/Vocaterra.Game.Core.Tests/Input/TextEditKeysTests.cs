using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Input;

/// <summary>The editing keys of the text boxes: each fires when it goes down, waits, then repeats while held, and the four are counted apart.</summary>
public sealed class TextEditKeysTests
{
    [Fact]
    public void AHeldKeyFiresAtOnceThenRepeatsAfterTheDelay()
    {
        var keys = new TextEditKeys();
        var held = Frame(GameKey.Back);
        var fired = Enumerable.Range(0, KeyRepeater.DelayFrames + (2 * KeyRepeater.IntervalFrames) + 1).Select(_ => keys.Step(held).Backspace).ToList();
        Assert.True(fired[0]);
        Assert.All(fired.Skip(1).Take(KeyRepeater.DelayFrames - 1), Assert.False);
        Assert.True(fired[KeyRepeater.DelayFrames]);
        Assert.True(fired[KeyRepeater.DelayFrames + KeyRepeater.IntervalFrames]);
        Assert.True(fired[KeyRepeater.DelayFrames + (2 * KeyRepeater.IntervalFrames)]);
        Assert.Equal(4, fired.Count(static f => f)); // at once, after the delay, and twice at the interval
    }

    [Fact]
    public void TheFourKeysAreCountedApartAndALetGoKeyStartsOver()
    {
        var keys = new TextEditKeys();
        Assert.Equal(new TextEdits(false, true, true, false), keys.Step(Frame(GameKey.Delete, GameKey.Left)));
        Assert.Equal(new TextEdits(false, false, false, true), keys.Step(Frame(GameKey.Delete, GameKey.Left, GameKey.Right)));
        Assert.Equal(new TextEdits(false, false, false, false), keys.Step(Frame()));
        Assert.Equal(new TextEdits(false, true, false, false), keys.Step(Frame(GameKey.Delete)));
    }

    private static TestInputFrame Frame(params GameKey[] keys) => TestInputFrame.Pressing(keys);
}
