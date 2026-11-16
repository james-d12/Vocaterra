using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>U08: each NPC's conversation lines ride in the save, so the panel scrolls back over earlier sessions.</summary>
public sealed class DialoguePagesSaveTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-dlg-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static NpcOpening Opening(string npc, string line) => new(npc, "prompt", [line], false, [], false);

    private static DialogueModel Model()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        return new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
    }

    [Fact]
    public void TheSpeakerNamesAreAStableFormat()
    {
        var names = Enum.GetValues<DialogueSpeaker>().ToDictionary(static s => s, static s => DialoguePageSave.SpeakerName(s));
        Assert.Equal(
            new Dictionary<DialogueSpeaker, string?>
            {
                [DialogueSpeaker.Player] = "player",
                [DialogueSpeaker.PlayerSpoken] = "player-spoken",
                [DialogueSpeaker.Note] = "note",
                [DialogueSpeaker.Correction] = "correction",
                [DialogueSpeaker.Accent] = "accent",
                [DialogueSpeaker.Guide] = "guide",
                [DialogueSpeaker.World] = "world",
                [DialogueSpeaker.Hint] = "hint",
                [DialogueSpeaker.English] = "english",
                [DialogueSpeaker.Coaching] = null,
            },
            names);
        foreach (var (speaker, name) in names.Where(static p => p.Value is not null))
        {
            Assert.Equal(speaker, DialoguePageSave.SpeakerOf(name!));
        }

        Assert.Null(DialoguePageSave.SpeakerOf("nobody"));
    }

    [Fact]
    public void SeveralNpcsPagesRoundTripThroughTheJsonAndBackIntoAModel()
    {
        var before = Model();
        before.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero");
        before.AddHint("una pista");
        before.AddNpcLine("Toma la botella.");
        before.Close();
        before.Open(Opening("baker", "¡Hola!"), "Panadera");
        before.Close();

        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var document = SaveGame.Capture(session, RouteScene.Build(), null, DialoguePageSave.Capture(before.Pages));
        var parsed = SaveGame.Parse(SaveGame.Serialize(document));
        Assert.Equal(["baker", "shopkeeper"], parsed.DialoguePages!.Select(static p => p.Npc));

        var after = Model();
        after.RestorePages(parsed.DialoguePages);
        Assert.Equal(before.Pages.Keys.Order(StringComparer.Ordinal), after.Pages.Keys.Order(StringComparer.Ordinal));
        foreach (var (npc, lines) in before.Pages)
        {
            Assert.Equal(lines, after.Pages[npc]);
        }
    }

    [Fact]
    public void ThePageIsCappedAtMaxLinesKeepingTheNewest()
    {
        var lines = Enumerable.Range(0, DialogueModel.MaxLines + 30).Select(static i => new DialogueLineSave("guide", $"línea {i}")).ToArray();
        var model = Model();
        model.RestorePages([new DialoguePageSave("baker", lines)]);
        Assert.Equal(DialogueModel.MaxLines, model.Pages["baker"].Count);
        Assert.Equal("línea 30", model.Pages["baker"][0].Text);

        var capture = DialoguePageSave.Capture(new Dictionary<string, List<DialogueLine>> { ["baker"] = lines.Select(static l => new DialogueLine(DialogueSpeaker.Guide, l.Text)).ToList() });
        Assert.Equal(DialogueModel.MaxLines, capture![0].Lines.Count);
        Assert.Equal("línea 229", capture[0].Lines[^1].Text);
    }

    [Fact]
    public void ASaveWithoutTheFieldLoadsWithEmptyPagesAndWritesNoField()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var text = SaveGame.Serialize(SaveGame.Capture(session, RouteScene.Build(), null, DialoguePageSave.Capture(Model().Pages)));
        Assert.DoesNotContain("ialoguePages", text, StringComparison.OrdinalIgnoreCase);
        Assert.Null(SaveGame.Parse(text).DialoguePages);

        var model = Model();
        model.Open(Opening("baker", "¡Hola!"), "Panadera");
        model.RestorePages(null);
        Assert.Empty(model.Pages);
    }

    [Fact]
    public void AnUnknownSpeakerNameIsSkippedOnLoad()
    {
        var model = Model();
        model.RestorePages([new DialoguePageSave("baker", [new DialogueLineSave("future-kind", "x"), new DialogueLineSave("guide", "¡Hola!")])]);
        Assert.Equal(["¡Hola!"], model.Pages["baker"].Select(static l => l.Text));
    }

    [Fact]
    public void TheServiceWritesThePanelsPagesAndThePanelScrollsBackOverThemAfterAReload()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var first = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
        first.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero");
        first.AddHint("una pista");
        first.AddNpcLine("Toma la botella.");
        first.Close();
        var saves = new SaveService(session, static () => RouteScene.Build(), static () => new SaveSettings(new SoundVolumes()), static _ => { }, first);
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "game.save");
        Assert.True(saves.Write(path, "test"));

        var reloaded = Model();
        reloaded.RestorePages(SaveGame.Parse(SaveFile.Read(path)).DialoguePages);
        reloaded.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero"); // the new session's greeting, appended to the restored page
        reloaded.ToggleExpanded();
        var texts = reloaded.PanelRows().Select(static r => r.Line.Text).ToArray();
        Assert.Contains("una pista", texts);
        Assert.Contains("Toma la botella.", texts);
        Assert.Equal(["¿Qué necesitas?", "una pista", "Toma la botella.", "¿Qué necesitas?"], reloaded.Lines.Select(static l => l.Text));
    }

    [Fact]
    public void TheSaveFixtureHoldsTheShopkeepersPage()
    {
        var document = SaveGame.Parse(File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", "save-v10.json")));

        var page = Assert.Single(document.DialoguePages!);
        Assert.Equal("shopkeeper", page.Npc);
        Assert.Contains(page.Lines, static l => l.Speaker == "player");
    }
}
