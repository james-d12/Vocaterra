using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.TestSupport;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The shared parts of the streamed world host's test classes (B28 split them so xUnit runs them in parallel): the folders a test makes in the temp folder and removes after it, and the helpers that settle a host and read the far store.</summary>
public abstract class StreamedWorldHostTestBase : IDisposable
{
    private readonly List<string> _folders = [];

    public void Dispose()
    {
        foreach (var folder in _folders.Where(Directory.Exists))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    protected string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vocaterra-host-test-" + Guid.NewGuid().ToString("N"));
        _folders.Add(folder);
        return folder;
    }

    protected static QualityPreset EditPreset() => QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(768) with { FarRealRadius = 64 };

    // A house block in the air over the ground beside the spawn.
    protected static (int X, int Y, int Z) HouseSpot(Scene scene) => ((int)MathF.Floor(scene.Spawn.Feet.X) + 3, (int)MathF.Floor(scene.Spawn.Feet.Y) + 2, (int)MathF.Floor(scene.Spawn.Feet.Z) + 3);

    protected static (float X, float Y, float Z) Eye(Scene scene) => (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);

    protected static Vocaterra.Game.Core.World.Lod.LodSection FarSection(StreamedWorldHost host, (int X, int Y, int Z) at) =>
        host.Far.World.Stack(0, at.X >> 5, at.Z >> 5).Sections[(at.Y >> 5) - host.Far.World.SyRange(0).Min];

    protected static Vocaterra.Game.Core.Voxel.BlockId FarBlock(StreamedWorldHost host, (int X, int Y, int Z) at) =>
        FarSection(host, at).BlockAt(Vocaterra.Game.Core.World.Lod.LodSection.Index(at.X & 31, at.Y & 31, at.Z & 31));

    protected static bool SettleHost(StreamedWorldHost host, (float X, float Y, float Z) eye) =>
        SettleGuard.Settle(limit => host.Settle(eye.X, eye.Y, eye.Z, _ => { }, limit), () => host.Residency.Stats.Installed + host.Far.Stats.Built + host.Ring.Done + host.EditedChunksTaken);

    protected static void Settled(StreamedWorldHost host, Scene scene)
    {
        var eye = Eye(scene);
        Assert.True(SettleHost(host, eye), "the host settles (no slice without progress)");
    }
}
