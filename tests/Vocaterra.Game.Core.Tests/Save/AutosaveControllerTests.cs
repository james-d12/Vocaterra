using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The autosave: a save when the policy says one is due and the game is at rest, a toast for it, and a failed write said and survived.</summary>
public sealed class AutosaveControllerTests : IDisposable
{
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

    [Fact]
    public void ASleepWritesTheEndingSessionWithoutAToastAndStartsThePolicyAgainAsALoadDoes()
    {
        var (autosave, lines, toasts) = Make(Path.Combine(_folder, "autosave.save"));
        IReadOnlyList<GameEvent> due = [new WorldEvent(5, 0, WorldEvents.TalkClosed, "guide", null)];

        autosave.WriteNow(20, eventsSeen: 1, "sleep"); // V12c: the session as it ends at the fire
        Assert.True(File.Exists(autosave.Path));
        Assert.StartsWith($"autosave: {autosave.Path} written at sleep (session 1, tick 0, ", Assert.Single(lines));
        Assert.Empty(toasts); // the morning's own toast says it
        Assert.Equal(20, autosave.LastSavedTick);

        autosave.Run(21, due, atRest: true); // the event was seen: nothing is due
        Assert.Single(lines);

        autosave.Restart(30, eventsSeen: 0); // the next session begins as a load begins it: from this tick, these events counted as new
        Assert.Equal(30, autosave.LastSavedTick);
        autosave.Run(31, due, atRest: true);
        Assert.Equal(2, lines.Count);
        Assert.Contains("written at dialogue closed", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AnAutosaveSwitchedOffWritesNothingAndOneSwitchedOnStartsItsPolicyFromNow()
    {
        var (autosave, lines, toasts) = Make(Path.Combine(_folder, "autosave.save"));
        Assert.True(autosave.Enabled);
        autosave.SetEnabled(false, 0, 0);
        autosave.Run(Due, [], atRest: true);
        autosave.WriteNow(Due, 0, "sleep");
        Assert.False(File.Exists(autosave.Path));
        Assert.Empty(lines);
        Assert.Empty(toasts);

        autosave.SetEnabled(true, Due, 0); // a long time had passed, but the policy begins again here
        autosave.Run(Due + 10, [], atRest: true);
        Assert.False(File.Exists(autosave.Path));
        autosave.Run(Due + Due, [], atRest: true);
        Assert.True(File.Exists(autosave.Path));
        Assert.Single(toasts);
    }

    private (AutosaveController Autosave, List<string> Lines, List<string> Toasts) Make(string path)
    {
        Directory.CreateDirectory(_folder);
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var lines = new List<string>();
        var toasts = new List<string>();
        var saves = new SaveService(session, () => RouteScene.Build(), () => new SaveSettings(new SoundVolumes()), lines.Add);
        return (new AutosaveController(new AutosavePolicy(0, 0), path, saves, lines.Add, toasts.Add, static () => 0), lines, toasts);
    }
}
