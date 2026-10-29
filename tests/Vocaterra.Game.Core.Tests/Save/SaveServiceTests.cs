using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The game's saves: the fresh scene is built once for them all, and the file written is the capture of the session.</summary>
public sealed class SaveServiceTests : IDisposable
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-saves-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheFreshSceneIsBuiltOnceAndTheFileIsTheCaptureOfTheSession()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var builds = 0;
        var lines = new List<string>();
        var settings = new SaveSettings(new SoundVolumes(), 125);
        var saves = new SaveService(session, () => { builds++; return RouteScene.Build(); }, () => settings, lines.Add);
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "game.save");

        Assert.True(saves.Write(path, "on F6"));
        Assert.True(saves.Write(path, "at the end of the run"));
        Assert.Equal(1, builds);
        Assert.Equal(SaveGame.Serialize(saves.Capture()), SaveFile.Read(path));
        Assert.Equal(1, builds);
        Assert.Equal(2, lines.Count);
        Assert.StartsWith($"save: {path} written on F6 (session 1, tick 0,", lines[0]);
        Assert.Equal(125, saves.Capture().Settings!.TextScale);
    }
}
