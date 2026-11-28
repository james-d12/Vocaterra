using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The game's saves: no second copy of the world is built or kept (B05), and the file written is the capture of the session.</summary>
public sealed class SaveServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-saves-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Given_ASession_When_ItIsSavedTwice_Then_TheFileIsTheCaptureOfTheSession()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var lines = new List<string>();
        var saves = new SaveService(session, lines.Add);
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "game.save");

        Assert.True(saves.Write(path, "on F6"));
        Assert.True(saves.Write(path, "at the end of the run"));
        Assert.Equal(SaveGame.Serialize(saves.Capture()), SaveFile.Read(path));
        Assert.Equal(2, lines.Count);
        Assert.StartsWith($"save: {path} written on F6 (session 1, tick 0,", lines[0]);
    }

    [Fact]
    public void Given_AHeldStackAndAFullBag_When_TheGameIsSavedAtTheQuit_Then_NothingIsWrittenAndNothingIsThrown()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var sim = session.Simulation;
        var limit = sim.Inventory.Items.Get(TestItems.Apple).MaxStack;
        for (var slot = 0; slot < sim.Inventory.SlotCount; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(TestItems.Apple, limit));
        }

        sim.Held.Put(0, new ItemStack(TestItems.Bread, 1));
        sim.Held.SetOrigin(0, 5);
        var lines = new List<string>();
        var saves = new SaveService(session, lines.Add);
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "game.save");

        var written = saves.Write(path, "at the end of the run");

        Assert.False(written);
        Assert.False(File.Exists(path));
        Assert.Single(lines);
        Assert.Contains("held", lines[0]);
        Assert.False(sim.Held.IsEmpty); // the stack stays held, never lost
    }

    [Fact]
    public void Given_ASessionOnTheValley_When_TheFirstSaveIsTaken_Then_NoCopyOfTheWorldIsAllocated()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var saves = new SaveService(session, static _ => { });
        _ = saves.Capture(); // warms the serializer-free paths, so the measure below is the save's own

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = saves.Capture();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The valley's blocks are 38 MB; the first save used to build a second copy (0.7 s) and keep it.
        Assert.True(allocated < 8_000_000, $"A capture allocated {allocated:N0} bytes.");
    }
}
