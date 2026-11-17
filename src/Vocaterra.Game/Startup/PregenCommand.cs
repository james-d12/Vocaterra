using System.Globalization;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Startup;

/// <summary>
/// <c>--pregen &lt;blocks&gt;</c> (B07d, D-game-34 item 29: "a world import or pre-generation command"): makes the far view's real sections of a seed's world round a point, out to a radius, on all the workers, prints its progress and exits with no window. The sections are kept
/// on disk under the world's own folder (<c>VOCATERRA_LOD_DIR</c>), so a run after it finds those footprints real and its far view over them is built from the store and not from the generator. Footprints already on disk are not made again, so the command can be stopped and
/// run again, or run for a bigger radius, and it only makes what is missing. The point is <c>--pregen-at x,z</c>, else where the run starts; the worker count is <c>--workers</c> or the saved Worker threads setting (Auto by default: the processor count less four, at most eight).
/// </summary>
public static class PregenCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="options">The launch options.</param>
    /// <param name="session">The session whose world is made (its header names the folder).</param>
    /// <param name="scene">The scene.</param>
    /// <param name="saved">The settings a loaded save holds, or <see langword="null"/>.</param>
    /// <param name="radius">The radius in blocks.</param>
    /// <param name="output">Where the progress goes.</param>
    /// <returns>The process's exit code.</returns>
    public static int Run(LaunchOptions options, VoxelSession session, Scene scene, SaveSettings? saved, int radius, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(output);
        if (!scene.Grid.IsStreamed)
        {
            Console.Error.WriteLine("--pregen needs the streamed world (it makes the far view's sections); --world valley has none.");
            return 2;
        }

        var settings = PlayerSettings.Resolve(saved, new LaunchSettings(Workers: options.Workers));
        var workers = WorkerPlan.For(options.Prefetch, options.Workers, scripted: false, settings.Workers, Environment.ProcessorCount);
        var preset = QualityPreset.For(settings.Quality).WithFarRange(0) with { FarRealRadius = radius };
        var (x, z) = options.PregenAt is { } at ? ((double)at.X + 0.5, (double)at.Z + 0.5) : (scene.Spawn.Feet.X + 0.5, scene.Spawn.Feet.Z + 0.5);
        var world = StreamedWorldHost.LodWorldName(session.Simulation.StateHeader);
        using var host = StreamedWorldHost.For(scene, preset, workers, world);
        if (host is null)
        {
            Console.Error.WriteLine("--pregen needs the streamed world.");
            return 2;
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"pregen: the far view's real sections of world {world} round {x:0},{z:0} out to {radius} blocks on {workers} worker thread(s); folder {FileLodPersistence.FolderFor(world)}"));
        var result = host.Pregenerate(x, z, radius, progress => output.WriteLine(progress.Line));
        var onDisk = result.Folder is { } folder && Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(static file => new FileInfo(file).Length) : 0L;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"pregen: done, {result.Footprints} footprints made (of {result.Planned} planned) in {result.Milliseconds / 1000.0:0.0} s, {(result.Milliseconds > 0 ? result.Footprints / (result.Milliseconds / 1000.0) : 0):0.0} a second; {onDisk / (1024.0 * 1024.0):0.0} MB on disk in {result.Folder}; {host.LodStoreSummary}"));
        return 0;
    }
}
