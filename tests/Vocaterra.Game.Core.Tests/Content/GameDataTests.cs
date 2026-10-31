using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>
/// The game's content in one load (CONTENT_PLAN C02): every game file read from the bundle's verified bytes, and the checks between the files, which report
/// every problem at once.
/// </summary>
public sealed class GameDataTests
{
    [Fact]
    public void TheBundlesContentLoadsAndHoldsTogether()
    {
        var data = GameData.Load(TestGame.Bundle, TestGame.Locale);

        Assert.Empty(GameDataValidator.Validate(data));
        Assert.Equal(22, data.Nouns.Nouns.Count); // V03 added moneda, manzana and hacha
        Assert.Equal(109, data.Phrases.Rows.Count); // V10 added El pueblo's fifteen, V11 Números y pan's sixteen, V12 Gente del pueblo's sixteen, V13 ¿Cómo es?'s sixteen
        Assert.Equal(63, data.Checks.Count);
        Assert.Equal(7, data.Lessons.Lessons.Count);
        Assert.Equal("prototype-valley", data.Scene.Name);
    }

    [Fact]
    public void TheSceneIsTheBundlesVerifiedFileNotASecondReadFromDisk()
    {
        // The layout is parsed from the bytes the core hashed against the manifest: the same layout as the file on disk, read once.
        var fromDisk = SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json"));

        Assert.Equal(GameData.HostText(TestGame.Bundle, GameData.ScenePath), File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json")));
        Assert.Equal(fromDisk.Sites.Select(static s => s.Name), TestGame.Data.Scene.Sites.Select(static s => s.Name));
        Assert.Equal(fromDisk.Npcs, TestGame.Data.Scene.Npcs);
    }

    [Fact]
    public void AFileTheBundleDoesNotHaveIsALoadErrorNamingIt()
    {
        var error = Assert.Throws<InvalidDataException>(() => GameData.HostText(TestGame.Bundle, "game/no-such-file.json"));

        Assert.Contains("game/no-such-file.json", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryProblemIsReportedAtOnceWithItsFile()
    {
        // A missing phrase explanation in the locale and a scene that places another NPC, which the lessons at the villager feel too: every problem is
        // listed, in the files' order.
        var files = Directory.GetFiles(Repo.Path("content", "locale", "en"), "*.json").Order(StringComparer.Ordinal)
            .Select(static f => (Path.GetFileName(f), File.ReadAllText(f).Replace("\"phrase.hola.explain\":", "\"phrase.hola-x.explain\":", StringComparison.Ordinal)));
        var scene = File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json"))
            .Replace("\"name\": \"villager\"", "\"name\": \"miller\"", StringComparison.Ordinal);
        var data = TestGame.Data.WithLocale(LocaleText.Parse("en", "h", files)).WithScene(SceneLayout.Parse(scene));

        var problems = GameDataValidator.Validate(data);

        Assert.Collection(
            problems,
            static p => Assert.Equal("locale en: the string 'phrase.hola.explain' is missing.", p),
            static p => Assert.Equal("locale en: the string 'phrase.hola-x.explain' is asked for by no content or code (an orphan).", p),
            static p => Assert.StartsWith("game/scene.json: the scene places the NPC 'miller'", p, StringComparison.Ordinal),
            static p => Assert.Equal("game/scene.json: the scene does not place the NPC 'villager'.", p),
            static p => Assert.Equal("game/lessons.json: the lesson 'personas' is said at 'villager', whom the scene does not place.", p),
            static p => Assert.Equal("game/lessons.json: the lesson 'ayer-y-manana' is said at 'villager', whom the scene does not place.", p),
            static p => Assert.Equal("game/lessons.json: the lesson 'el-pueblo' is said at 'villager', whom the scene does not place.", p), // the signpost step (V10)
            static p => Assert.Equal("game/lessons.json: the lesson 'gente-del-pueblo' is said at 'villager', whom the scene does not place.", p)); // its first and last parts (V12)
        var error = Assert.Throws<InvalidDataException>(() => GameData.Checked(data));
        Assert.Contains("(8 problems)", error.Message, StringComparison.Ordinal);
        Assert.Contains("hola-x", error.Message, StringComparison.Ordinal);
    }
}
