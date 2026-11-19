using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// B47: the owner flew over the far land at 190 blocks a second and saw sky through it (grids of strips, square holes, a stepped line along the edge of the chunks). The rule: every far leaf the chunks do not show is drawn, by its own tile or by a stand-in's window or whole,
/// whatever the reason its own tile is missing (not asked for, held by the near-first gate, being built, waiting for the ring, dropped), and no ground at the edge of the voxel zone is left to the sky.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewFlightTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    private static readonly FarViewSettings Wide = new(2048, MaxTiles: 600, TreeLevels: 1, RealRadius: 192);
    private const int Vox = 192;
    private const int Cell = 32;
    private const int Meshing = 20;

    // Whether the chunks show a rectangle of ground, as the far view decides it for a leaf (IsNeeded): wholly inside the zone and every column of it drawn.
    private static bool ChunksShow(int x0, int z0, int x1, int z1, double eyeX, double eyeZ, Func<int, int, bool> ready)
    {
        var (dx, dz) = (Math.Max(Math.Abs(eyeX - x0), Math.Abs(eyeX - x1)), Math.Max(Math.Abs(eyeZ - z0), Math.Abs(eyeZ - z1)));
        if (Math.Sqrt((dx * dx) + (dz * dz)) >= Wide.InnerFraction * Vox)
        {
            return false;
        }

        for (var cz = z0 >> 4; cz <= (z1 - 1) >> 4; cz++)
        {
            for (var cx = x0 >> 4; cx <= (x1 - 1) >> 4; cx++)
            {
                if (!ready(cx, cz))
                {
                    return false;
                }
            }
        }

        return true;
    }

    [Theory]
    [InlineData(true, 3.0, false)]
    [InlineData(false, 3.0, false)]
    [InlineData(true, 3.0, true)]
    public void Given_AFastEyeAndTheGateArming_When_EveryFrameIsAssembled_Then_NoLeafTheChunksDoNotShowIsLeftToTheSky(bool gl, double blocksPerFrame, bool withRing)
    {
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);
        var gate = new NearFirstGate(NearHold.All);
        view.HoldFine = () => gate.HoldsFine;
        view.HoldCoarse = () => gate.HoldsCoarse;
        LodRing? ring = null;
        if (withRing)
        {
            var world = StreamedWorlds.Composition.World;
            ring = new LodRing(view.World, world, pool, new ColumnBandCache(world.Bands)) { Hold = () => gate.HoldsFine };
            view.SetRingWait(ring.Awaits);
        }
        var (eyeX, eyeZ) = (192.0, 192.0);
        var readyAt = new Dictionary<(int, int), int>();
        var frame = 0;
        bool Ready(int cx, int cz)
        {
            var (dx, dz) = ((cx * 16) + 8 - eyeX, (cz * 16) + 8 - eyeZ);
            if ((dx * dx) + (dz * dz) > (double)Vox * Vox)
            {
                _ = readyAt.Remove((cx, cz));
                return false;
            }

            if (!readyAt.TryGetValue((cx, cz), out var at))
            {
                readyAt[(cx, cz)] = at = frame + Meshing;
            }

            return frame >= at;
        }

        _ = view.UsePath(gl, static _ => { });
        view.SetNearReady(Ready);
        gate.Arm(0, eyeX, eyeZ);
        var sky = new Dictionary<string, int>();
        var skyFrames = 0;
        var skyAt = new List<int>();
        var selectedAt = (X: eyeX, Z: eyeZ);
        var firstSky = -1;
        const int FlightFrom = 300;
        var gateOpenedAt = -1;
        for (frame = 0; frame < 1500; frame++)
        {
            foreach (var tile in view.PendingTiles.ToList())
            {
                tile.Adopt(tile.Pending!);
                tile.Pending = null;
                tile.OnCard = true;
            }

            if (frame >= FlightFrom)
            {
                eyeX += blocksPerFrame;
            }

            _ = gate.Observe(frame * 16.7, () => Enumerable.Range(-8, 17).All(dz => Enumerable.Range(-8, 17).All(dx => Ready(((int)eyeX >> 4) + dx, ((int)eyeZ >> 4) + dz))), (eyeX, eyeZ));
            ring?.Update(eyeX, eyeZ, 192);
            if (frame >= FlightFrom && gateOpenedAt < 0 && !gate.IsClosed)
            {
                gateOpenedAt = frame;
            }

            view.Update(eyeX, eyeZ, Vox, static _ => { }, 2.0);
            foreach (var tile in view.PendingTiles.ToList())
            {
                tile.Adopt(tile.Pending!); // the renderer's upload in the frame's draw, and it assembles again (FarView.Reassemble)
                tile.Pending = null;
                tile.OnCard = true;
            }

            view.Reassemble();

            var owner = new HashSet<(int, int)>();
            for (var i = 0; i < view.Draw.Count; i++)
            {
                var tile = view.Draw[i];
                var standIn = i < view.FallbackCount;
                var rects = standIn && tile.Windows is { } windows ? windows.Select(static w => (w.MinX, w.MinZ, w.MaxX, w.MaxZ)).ToList() : [(tile.Key.MinX(Wide), tile.Key.MinZ(Wide), tile.Key.MinX(Wide) + Wide.SizeOf(tile.Key.Level), tile.Key.MinZ(Wide) + Wide.SizeOf(tile.Key.Level))];
                foreach (var (x0, z0, x1, z1) in rects)
                {
                    for (var gz = z0 / Cell; gz < z1 / Cell; gz++)
                    {
                        for (var gx = x0 / Cell; gx < x1 / Cell; gx++)
                        {
                            _ = owner.Add((gx, gz));
                        }
                    }
                }
            }

            if (frame < FlightFrom)
            {
                continue;
            }

            var frameSky = false;
            if (Math.Abs(eyeX - selectedAt.X) >= 8 || Math.Abs(eyeZ - selectedAt.Z) >= 8)
            {
                selectedAt = (eyeX, eyeZ); // the view chooses its leaves when the eye has moved a cell: what it draws is for those
            }

            foreach (var leaf in FarTileSelector.Select(Wide, selectedAt.X, selectedAt.Z, Vox, true, 1.0))
            {
                var (x0, z0, size) = (leaf.MinX(Wide), leaf.MinZ(Wide), Wide.SizeOf(leaf.Level));
                if (ChunksShow(x0, z0, x0 + size, z0 + size, eyeX, eyeZ, Ready))
                {
                    continue;
                }

                var covered = true;
                for (var gz = z0 / Cell; gz < (z0 + size) / Cell && covered; gz++)
                {
                    for (var gx = x0 / Cell; gx < (x0 + size) / Cell; gx++)
                    {
                        if (!owner.Contains((gx, gz)))
                        {
                            covered = false;
                            break;
                        }
                    }
                }

                if (!covered)
                {
                    frameSky = true;
                    var near = Math.Sqrt(Math.Pow((x0 + (size / 2.0)) - eyeX, 2) + Math.Pow((z0 + (size / 2.0)) - eyeZ, 2)) < 1.5 * Vox;
                    var label = $"L{leaf.Level}{(near ? " near" : " far")}{(gate.IsClosed ? " gate closed" : string.Empty)}";
                    sky[label] = sky.GetValueOrDefault(label) + 1;
                }
            }

            if (frameSky)
            {
                skyFrames++;
                skyAt.Add(frame);
                if (firstSky < 0)
                {
                    firstSky = frame;
                }
            }
        }

        Output.WriteLine($"gl {gl}, ring {withRing}, {blocksPerFrame} blocks a frame: frames with a leaf left to the sky: {skyFrames} of {1500 - FlightFrom}; the gate opened at frame {gateOpenedAt}; {gate.Line}");
        foreach (var (label, count) in sky.OrderBy(static kv => kv.Key))
        {
            Output.WriteLine($"  {label}: {count} leaf-frames");
        }

        // The near view cannot be finished by an eye that flies on (the chunks never catch up): the gate lets the far work go once the eye has left the place it was closed at, and does not hold for its whole timeout.
        Assert.InRange(gateOpenedAt, FlightFrom, FlightFrom + (int)(NearFirstGate.LeaveBlocks / blocksPerFrame) + 5);
        // The far land has to catch up with a view that has moved on (the build throughput of the workers: the test runs on one thread, two milliseconds a frame, and with the ring beside it the ring's chunk jobs too); after that no leaf is left to the sky for the rest of the flight.
        var catchUp = gateOpenedAt + (withRing ? 360 : 120);
        Assert.DoesNotContain(skyAt, f => f > catchUp);
    }
}
