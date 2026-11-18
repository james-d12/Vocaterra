using System.Diagnostics;
using System.IO.Compression;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>Which NPCs show their name plate (<see cref="NamePlates.Select"/>, V22): the one the player faces within talking range and the beacon's, no other.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class NamePlatesTests
{
    private static readonly NpcFigure Guide = new("guide", "Luna", new BlockPos(179, 42, 187), 0);
    private static readonly NpcFigure Baker = new("baker", "Panadera", new BlockPos(281, 38, 208), 0);
    private static readonly NpcFigure Villager = new("villager", "Aldeano", new BlockPos(293, 37, 221), 0);
    private static readonly IReadOnlyList<NpcFigure> All = [Guide, Baker, Villager];

    [Fact]
    public void NothingFacedAndNoBeaconShowsNoPlate() => Assert.Empty(NamePlates.Select(All, null, null, null));

    [Fact]
    public void AnNpcInRangeButNotFacedShowsNoPlate() => Assert.Empty(NamePlates.Select(All, null, new BlockPos(0, 0, 0), null)); // Aimed is null when the crosshair is not on anyone, whoever stands near

    [Fact]
    public void TheFacedNpcShowsOnePlate() => Assert.Equal([Baker], NamePlates.Select(All, Baker, null, null));

    [Fact]
    public void TheBeaconsNpcShowsItsPlateFromFarAway() => Assert.Equal([Villager], NamePlates.Select(All, null, Villager.Feet, null));

    [Fact]
    public void TheFacedNpcThatIsTheBeaconsShowsOnePlate() => Assert.Equal([Baker], NamePlates.Select(All, Baker, Baker.Feet, null));

    [Fact]
    public void AFacedNpcAndAnotherBeaconNpcShowTwoPlatesFacedFirst() => Assert.Equal([Baker, Villager], NamePlates.Select(All, Baker, Villager.Feet, null));

    [Fact]
    public void ABeaconOnNoNpcShowsNoPlate() => Assert.Empty(NamePlates.Select(All, null, new BlockPos(176, 42, 186), null));

    [Fact]
    public void TheSpeakerHasNoPlateBecauseTheBubbleHasHisName() => Assert.Equal([Villager], NamePlates.Select(All, Baker, Villager.Feet, "baker"));

    // V22's depth test is the graphics card's (the plate reads the depth buffer), so the proof is a picture: the baker's plate drawn from the square, and left out from the
    // north of the bakery, where the bakery is between the camera and her (the beacon is on her in both, the toast of the request gone after five seconds: the Lessons page's "Ask for a class" for El mercado sends it to her, so Select picks her in both). The test runs the game window, so it needs a
    // display (DISPLAY) and the built game; without one it does nothing, as the other tests of tools that are not there (PlaytestReportTests).
    [Fact]
    public async Task TheBeaconsNpcBehindABuildingHasNoPlateDrawnAndInTheOpenShesGotOne()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Vocaterra.Game.dll");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) || !File.Exists(dll))
        {
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), "vocaterra-plates-" + Guid.NewGuid().ToString("N"));
        try
        {
            _ = Directory.CreateDirectory(folder);
            // B28: the two game processes (each starts, builds the world and waits its seconds) run at the same time, not one after the other; the folders and the pictures are apart.
            var inTheOpen = Task.Run(() => Shot(dll, folder, "open", "281,222", "0,4"));        // the square, looking north at the bakery's door: the baker stands in front of it
            var behind = Task.Run(() => Shot(dll, folder, "behind", "281,196", "180,4"));       // north of the bakery, looking south: the bakery is between
            Assert.NotEmpty(PlateRows(await inTheOpen));
            Assert.Empty(PlateRows(await behind));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static byte[] Shot(string dll, string folder, string name, string teleport, string look)
    {
        var png = Path.Combine(folder, name + ".png"); // the far view is off: these pictures are about name plates, and a settled run would wait for thousands of far tiles
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[]
        {
            dll, "--seed", "42", "--mute", "--session-dir", Path.Combine(folder, "session-" + name), "--content", GameBundle.ResolveDirectory(),
            "--far-view", "off", "--skip-to-session", "10", "--book-practise", "el-mercado", "--wait", "5", "--teleport", teleport, "--look", look, "--screenshot", png, "--frames", "20",
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit((int)HangGuard.Scale(120_000)), HangGuard.Hung("the render process did not exit", 120_000));
        Assert.Equal(0, process.ExitCode);
        return File.ReadAllBytes(png);
    }

    // The rows (below the objective strip) where a plate's gold border runs for 30 pixels or more, leaving out the two rows of the beacon's label box (21 rows apart), which
    // has the same gold: what is left is the plates' borders.
    private static List<(int Y, int X)> PlateRows(byte[] png)
    {
        var (width, height, bytesPerPixel, rows) = Decode(png);
        var runs = new List<(int Y, int X, int Length)>();
        for (var y = 60; y < height; y++)
        {
            var x = 0;
            while (x < width)
            {
                var start = x;
                while (x < width && rows[y][(x * bytesPerPixel)] == 255 && rows[y][(x * bytesPerPixel) + 1] == 214 && rows[y][(x * bytesPerPixel) + 2] == 64)
                {
                    x++;
                }

                if (x - start >= 30)
                {
                    runs.Add((y, start, x - start));
                }

                x = Math.Max(x, start + 1);
            }
        }

        return [.. runs.Where(r => !runs.Any(o => o.X == r.X && o.Length == r.Length && Math.Abs(o.Y - r.Y) == 21)).Select(static r => (r.Y, r.X))];
    }

    private static (int Width, int Height, int BytesPerPixel, byte[][] Rows) Decode(byte[] png)
    {
        int width = 0, height = 0, bytesPerPixel = 0;
        using var data = new MemoryStream();
        for (var position = 8; position < png.Length;)
        {
            var length = (png[position] << 24) | (png[position + 1] << 16) | (png[position + 2] << 8) | png[position + 3];
            var type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            if (type == "IHDR")
            {
                width = (png[position + 8] << 24) | (png[position + 9] << 16) | (png[position + 10] << 8) | png[position + 11];
                height = (png[position + 12] << 24) | (png[position + 13] << 16) | (png[position + 14] << 8) | png[position + 15];
                bytesPerPixel = png[position + 17] == 6 ? 4 : 3; // 8-bit RGBA or RGB, the screenshot's two forms
            }
            else if (type == "IDAT")
            {
                data.Write(png, position + 8, length);
            }

            position += 12 + length;
        }

        data.Position = 0;
        using var inflate = new ZLibStream(data, CompressionMode.Decompress);
        var raw = new MemoryStream();
        inflate.CopyTo(raw);
        var bytes = raw.ToArray();
        var stride = width * bytesPerPixel;
        var rows = new byte[height][];
        var previous = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = bytes[y * (stride + 1)];
            var line = new byte[stride];
            Array.Copy(bytes, (y * (stride + 1)) + 1, line, 0, stride);
            for (var x = 0; x < stride; x++)
            {
                int a = x >= bytesPerPixel ? line[x - bytesPerPixel] : 0, b = previous[x], c = x >= bytesPerPixel ? previous[x - bytesPerPixel] : 0;
                var p = a + b - c;
                int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                line[x] = (byte)(line[x] + filter switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => pa <= pb && pa <= pc ? a : pb <= pc ? b : c,
                    _ => 0,
                });
            }

            rows[y] = line;
            previous = line;
        }

        return (width, height, bytesPerPixel, rows);
    }
}
