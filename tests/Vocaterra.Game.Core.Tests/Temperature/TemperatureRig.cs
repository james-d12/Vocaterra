using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>Situations for the temperature tests: the sky at an hour of the shipped day and in a kind of the shipped weather, a place under a roof or beside a lit ring.</summary>
internal static class TemperatureRig
{
    public static TemperatureData Data => TestGame.Data.Temperature;

    /// <summary>The shipped terms in a place.</summary>
    public static PlaceTemperature Place => new(TemperatureTerms.Standard(Data));

    /// <summary>The sky at an hour with a kind of the shipped weather in it (no kind named in the code under test: the test names it, as the data does).</summary>
    public static AtmosphereSnapshot Sky(double hour, string kind)
    {
        var clock = new DayClock(TestGame.Data.Day);
        clock.Freeze(hour);
        return new AtmosphereSnapshot(hour, clock.PhaseAt(0), clock.DaylightAt(0), TestGame.Data.Weather.Kind(kind).Channels, kind, null);
    }

    /// <summary>A situation in the open, with no fire.</summary>
    public static TemperatureSituation Open(AtmosphereSnapshot sky) => new(sky, new BlockPos(5, 10, 5), 0, null);

    /// <summary>A situation under a roof over every column round the feet.</summary>
    public static TemperatureSituation Covered(AtmosphereSnapshot sky) => Open(sky) with { CoveredShare = 1 };

    /// <summary>A grid with one ring of eight stones round (8, 10, 8), lit by a torch on a stone of its rim when asked.</summary>
    public static FireRings Ring(bool lit)
    {
        var grid = new VoxelGrid(32, 32, 32);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if (dx != 0 || dz != 0)
                {
                    grid.SetBlock(8 + dx, 10, 8 + dz, BlockId.FireRing);
                }
            }
        }

        if (lit)
        {
            grid.SetBlock(9, 11, 9, BlockId.Torch);
        }

        return new FireRings(new Scene("ring", 0, grid, new SpawnPoint(new BlockPos(8, 11, 8), 0), [], new SortedDictionary<string, BlockPos>(StringComparer.Ordinal), 0, 0));
    }

    /// <summary>A situation at a distance from the ring's middle (8, 10, 8) along x.</summary>
    public static TemperatureSituation Beside(AtmosphereSnapshot sky, FireRings fires, int distance) => new(sky, new BlockPos(8 + distance, 10, 8), 0, fires);
}
