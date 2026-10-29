using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
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
        var data = GameData.Load(TestGame.Bundle);

        Assert.Empty(GameDataValidator.Validate(data));
        Assert.Equal(19, data.Nouns.Nouns.Count);
        Assert.Equal(46, data.Phrases.Rows.Count);
        Assert.Equal(31, data.Checks.Count);
        Assert.Equal(3, data.Lessons.Lessons.Count);
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
        // Two problems in the English text and one in the scene, which the lessons at the villager feel too: every one is listed, in the files' order.
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "english.json"))
            .Replace("\"hola\": \"Say this", "\"hola-x\": \"Say this", StringComparison.Ordinal)
            .Replace("\"npcNames\": {", "\"npcNames\": { \"innkeeper\": \"The innkeeper\",", StringComparison.Ordinal);
        var scene = File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json"))
            .Replace("\"name\": \"villager\"", "\"name\": \"miller\"", StringComparison.Ordinal);
        var data = TestGame.Data.WithEnglish(LocaleText.Parse(json)).WithScene(SceneLayout.Parse(scene));

        var problems = GameDataValidator.Validate(data);

        Assert.Collection(
            problems,
            static p => Assert.Equal("game/english.json: the explanation names the phrase 'hola-x', which is not in the phrase sheet.", p),
            static p => Assert.StartsWith("game/english.json: npcNames names 'innkeeper'", p, StringComparison.Ordinal),
            static p => Assert.StartsWith("game/scene.json: the scene places the NPC 'miller'", p, StringComparison.Ordinal),
            static p => Assert.Equal("game/scene.json: the scene does not place the NPC 'villager'.", p),
            static p => Assert.Equal("game/lessons.json: the lesson 'personas' is said at 'villager', whom the scene does not place.", p),
            static p => Assert.Equal("game/lessons.json: the lesson 'ayer-y-manana' is said at 'villager', whom the scene does not place.", p));
        var error = Assert.Throws<InvalidDataException>(() => GameData.Checked(data));
        Assert.Contains("(6 problems)", error.Message, StringComparison.Ordinal);
        Assert.Contains("hola-x", error.Message, StringComparison.Ordinal);
    }
}
