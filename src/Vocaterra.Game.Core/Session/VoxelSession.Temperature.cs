using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Session;

// Temperature and cold (I06, inventory-plan 3.7): where the player stands and what they wear give a feeling, and a cold band slows the walk. The place's temperature is the sum of
// the Temperature folder's terms; the warmth comes through an IWarmthSource: the player's stat sheet (I03a), which the worn equipment fills. The reading is derived each tick from the stored time, the weather
// and the world, so nothing here is saved. Cold never harms.
public sealed partial class VoxelSession
{
    private PlayerTemperature? _temperature;

    /// <summary>
    /// The player's temperature: the place's, the warmth, the feeling and its band, updated once a tick by <see cref="Step"/>. <see cref="PlayerTemperature.HeldTenths"/> holds the
    /// place's value (<c>--temperature</c>) and <see cref="PlayerTemperature.Warmth"/> is the stat sheet's warmth (I03a).
    /// </summary>
    public PlayerTemperature Temperature => _temperature ??= new PlayerTemperature(Data.Temperature, new PlaceTemperature(TemperatureTerms.Standard(Data.Temperature)), new StatWarmth(() => Simulation.Stats));

    /// <summary>
    /// Reads the temperature for this tick and gives the factor the band's cold sets on the walking speed: 1 with the needs off (a test aid that keeps the language runs' baselines,
    /// as the needs' own effects do) and while the feeling is warm or cool. The reading itself is always kept, for the debug overlay.
    /// </summary>
    private double ReadTemperature()
    {
        var reading = Temperature.Update(TemperatureNow());
        return NeedsEnabled ? reading.Band.WalkFactor : 1.0;
    }

    // The place and the moment for the terms: the sky now, the block under the feet, the share of cover round them (the rain's own cover query) and the fire rings.
    private TemperatureSituation TemperatureNow()
    {
        var player = Simulation.Player;
        const double U = VoxelPhysics.UnitsPerBlock;
        var covered = ListenerShelter.CoveredShare(Simulation.Grid.HighestBlock, new Listener(player.CenterX / U, player.MinY / U, player.CenterZ / U, player.Yaw));
        return new TemperatureSituation(Simulation.Atmosphere, player.FeetBlock, covered, Fires);
    }
}
