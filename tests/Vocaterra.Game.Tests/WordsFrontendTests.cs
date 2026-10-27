using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests;

/// <summary>The P4 front-end pieces that need no GPU: the greyscale copy, the journal's rows and model, the toasts, the scripted cursor and the launch options.</summary>
public sealed class WordsFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static VoxelSession NewSession() => RouteScene.NewSession(Bundle.Value, Nouns.Value, CreditRules.FullTyped);

    [Fact]
    public void ToastsShowInOrderForFourSecondsEach()
    {
        var toasts = new ToastQueue();
        toasts.Show("first");
        toasts.Show("second");

        Assert.Equal("first", toasts.Current(0)!.Text);
        Assert.Equal("first", toasts.Current(ToastQueue.Duration - 1)!.Text);
        Assert.Equal("second", toasts.Current(ToastQueue.Duration)!.Text);
        Assert.Equal(0, toasts.Waiting);
        Assert.Null(toasts.Current(ToastQueue.Duration * 2));
    }

}
