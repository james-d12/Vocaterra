using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>T04 (D-game-27 items 2 and 3): the save names its language, a load of another language's save or of an older version is refused, and each language has its own profile folder.</summary>
public sealed class SaveLanguageTests
{
    private static string SaveOf() => SaveGame.Save(TestGame.NewSession(), TestGame.Route(TestGame.Seed));

    [Fact]
    public void TheSaveCarriesTheBundlesLanguageAndRoundTrips()
    {
        var text = SaveOf();

        var document = SaveGame.Parse(text);

        Assert.Equal(9, SaveGame.CurrentVersion);
        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.Equal("es", document.Language);
        Assert.Equal(TestGame.Bundle.LanguageId, document.Language);
        Assert.Contains("\"language\":\"es\"", text, StringComparison.Ordinal);
        Assert.Equal(text, SaveGame.Serialize(document));
        var loaded = SaveGame.Load(text, TestGame.Data, seed => TestGame.Route(seed));
        Assert.Equal("es", SaveGame.Parse(SaveGame.Save(loaded, TestGame.Route(TestGame.Seed))).Language);
    }

    [Fact]
    public void ASaveOfAnotherLanguageIsRefusedWithAClearMessage()
    {
        var german = SaveGame.Parse(SaveOf()) with { Language = "de" };

        var refused = Assert.Throws<SaveException>(() => SaveGame.Check(german, TestGame.Bundle));

        Assert.Contains("'de'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("'es'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("saves/de/", refused.Message, StringComparison.Ordinal);
        Assert.Throws<SaveException>(() => SaveGame.Load(SaveGame.Serialize(german), TestGame.Data, seed => TestGame.Route(seed)));
    }

    [Fact]
    public void ASaveWithNoLanguageIsRefused()
    {
        var text = SaveOf().Replace("\"language\":\"es\",", string.Empty, StringComparison.Ordinal);

        Assert.Throws<SaveException>(() => SaveGame.Parse(text));
    }

    [Fact]
    public void ASaveOfVersionEightIsRefusedWithAClearMessage()
    {
        var old = SaveGame.Parse(SaveOf()) with { Version = 8 };

        var refused = Assert.Throws<SaveException>(() => SaveGame.Parse(SaveGame.Serialize(old)));

        Assert.Contains("version 8", refused.Message, StringComparison.Ordinal);
        Assert.Contains("language", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Start a new game", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EachLanguageHasItsOwnProfileFolder()
    {
        var root = Path.Combine("data", "saves");

        Assert.Equal(Path.Combine(root, "es"), SaveProfile.ProfileFolder(root, "es"));
        Assert.Equal(Path.Combine(root, "de"), SaveProfile.ProfileFolder(root, "de"));
        Assert.Equal(Path.Combine(root, "es", "autosave.save"), SaveProfile.AutosavePath(SaveProfile.ProfileFolder(root, "es")));
        Assert.Equal(Path.Combine(root, "es", "manual.save"), SaveProfile.ManualPath(SaveProfile.ProfileFolder(root, "es")));
        Assert.EndsWith(Path.Combine("vocaterra", "saves"), SaveProfile.DefaultRoot, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => SaveProfile.ProfileFolder(root, "../es"));
    }

    [Fact]
    public void AFirstSaveCreatesTheProfileFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "vocaterra-t04-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = SaveProfile.AutosavePath(SaveProfile.ProfileFolder(root, "es"));

            SaveFile.Write(path, SaveOf());

            Assert.True(File.Exists(path));
            Assert.Equal("es", SaveGame.Parse(File.ReadAllText(path)).Language);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TheLearnersRecordAcceptsTheNeuterGender()
    {
        var row = new WordRow("Haus", "noun", Genders.Neuter, 2, 1, [], new WordUse(1, 1, 1, "Das Haus."), new WordUse(2, 1, 2, "Ein Haus."));

        var words = LearnerWords.FromRows([row]);

        Assert.Equal("n", Assert.Single(words.Rows).Gender);
        Assert.True(Genders.IsKnown("n"));
        Assert.Equal(["m", "f", "n"], Genders.All);
        Assert.False(Genders.IsKnown("fem"));
        Assert.Throws<InvalidDataException>(() => LearnerWords.FromRows([row with { Gender = "x" }]));
    }
}
