using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Reporting;

/// <summary>The crash handler's view of the game: its state row by row, and the last-chance save, refused while a dialogue is open.</summary>
public sealed class CrashSourceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-crash-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheStateHasARowForEachPartAndNoNotesWhenEveryRowIsRead()
    {
        var (crash, session, _) = Make();
        var state = crash.State();
        Assert.Equal(session.Day, state.Session);
        Assert.Equal(0L, state.Tick);
        Assert.Equal(
            ["Bundle", "Locale", "Seed", "Position", "Objective", "Lesson", "Dialogue open", "Open panel", "Turns on the worker", "Last autosave tick"],
            state.Rows.Select(static r => r.Item));
        Assert.Equal("42", state.Rows.Single(static r => r.Item == "Seed").Value);
        Assert.Equal("off", state.Rows.Single(static r => r.Item == "Last autosave tick").Value);
        Assert.Null(state.Note);
        Assert.Equal(_folder, crash.SessionDirectory);
        Assert.Equal("reports-here", crash.ReportDirectory);
    }

    [Fact]
    public void TheLastChanceSaveIsWrittenAtRestAndRefusedWithADialogueOpen()
    {
        var (crash, session, dialogue) = Make();
        var path = Path.Combine(_folder, "crash.save");
        Assert.Null(crash.LastChanceSave(path));
        Assert.Equal(session.WorldSeed, SaveGame.Parse(SaveFile.Read(path)).WorldSeed);

        dialogue.Open();
        Assert.Equal("a dialogue was open, and a save holds no open dialogue", crash.LastChanceSave(path));
    }

    private (CrashSource Crash, VoxelSession Session, DialogueModel Dialogue) Make()
    {
        Directory.CreateDirectory(_folder);
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
        var saves = new SaveService(session, _ => { });
        return (new CrashSource(_folder, "reports-here", session, dialogue, new ReportPanel(), autosave: null, saves), session, dialogue);
    }
}
