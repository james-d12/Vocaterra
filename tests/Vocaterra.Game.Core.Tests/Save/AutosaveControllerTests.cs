using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The autosave: a save when the policy says one is due and the game is at rest, a toast for it, and a failed write said and survived.</summary>
public sealed class AutosaveControllerTests : IDisposable
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));
    private static readonly long Due = (AutosavePolicy.IntervalSeconds * 60L) + 1; // five minutes of play after the start
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-autosave-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ADueSaveIsTakenOnlyAtRestAndShownWithAToast()
    {
        var (autosave, lines, toasts) = Make(Path.Combine(_folder, "autosave.save"));
        IReadOnlyList<GameEvent> events = [];

        autosave.Run(10, events, atRest: true);
        autosave.Run(Due, events, atRest: false);
        Assert.Empty(lines);
        Assert.False(File.Exists(autosave.Path));

        autosave.Run(Due, events, atRest: true);
        Assert.True(File.Exists(autosave.Path));
        Assert.StartsWith($"autosave: {autosave.Path} written at ", Assert.Single(lines));
        Assert.Equal([AutosaveController.SavedToastId], toasts);
        Assert.Equal(Due, autosave.LastSavedTick);
    }

    [Fact]
    public void AWriteThatFailsIsSaidAndTheGameGoesOn()
    {
        var path = Path.Combine(_folder, "autosave.save");
        Directory.CreateDirectory(path + ".tmp"); // a folder stands where the temporary file would be
        var (autosave, lines, toasts) = Make(path);

        autosave.Run(Due, [], atRest: true);
        Assert.StartsWith("autosave: not written at ", Assert.Single(lines));
        Assert.Empty(toasts);
    }

    private (AutosaveController Autosave, List<string> Lines, List<string> Toasts) Make(string path)
    {
        Directory.CreateDirectory(_folder);
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var lines = new List<string>();
        var toasts = new List<string>();
        var saves = new SaveService(session, () => RouteScene.Build(), () => new SaveSettings(new SoundVolumes()), lines.Add);
        return (new AutosaveController(new AutosavePolicy(0, 0), path, saves, lines.Add, toasts.Add, static () => 0), lines, toasts);
    }
}
