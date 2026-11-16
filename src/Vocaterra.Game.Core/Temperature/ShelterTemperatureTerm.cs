namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// A roof's shelter (I06): warmer under cover, by the bonus in proportion to the share of the columns round the feet that are covered (<see cref="TemperatureSituation.CoveredShare"/>,
/// <see cref="Voxel.OverheadCover"/>: the rain's own definition of a roof). Data says whether it counts only while something falls. From <c>game/temperature.json</c>.
/// </summary>
public sealed class ShelterTemperatureTerm : ITemperatureTerm
{
    private readonly int _bonus;
    private readonly bool _onlyInPrecipitation;

    /// <summary>A shelter term.</summary>
    /// <param name="bonusTenths">The warmth of full cover, in tenths of a degree.</param>
    /// <param name="onlyInPrecipitation">Whether the cover counts only while precipitation falls.</param>
    public ShelterTemperatureTerm(int bonusTenths, bool onlyInPrecipitation)
    {
        _bonus = bonusTenths;
        _onlyInPrecipitation = onlyInPrecipitation;
    }

    /// <inheritdoc />
    public string Name => "shelter";

    /// <inheritdoc />
    public int TenthsAt(in TemperatureSituation situation)
    {
        if (_onlyInPrecipitation && Permille.Of(situation.Atmosphere.Channels.PrecipitationIntensity) == 0)
        {
            return 0;
        }

        return Permille.Times(_bonus, Permille.Of(situation.CoveredShare));
    }
}
