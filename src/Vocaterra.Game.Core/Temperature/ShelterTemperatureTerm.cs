namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// A roof's shelter (I06): warmer under cover, by the bonus in proportion to the share of the columns round the feet that are covered (<see cref="ITemperatureContext.CoveredShare"/>,
/// <see cref="Voxel.OverheadCover"/>: the rain's own definition of a roof). Data says whether it counts only while something falls. I06c: it fades out underground with the sky it shelters from. From <c>game/temperature.json</c>.
/// </summary>
public sealed class ShelterTemperatureTerm : ITemperatureTerm
{
    /// <summary>The term's id in the data and the registry.</summary>
    public const string Id = "shelter";

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
    public string Name => Id;

    /// <summary>Reads the term's row.</summary>
    /// <param name="parameters">The row's parameters.</param>
    public static ITemperatureTerm From(TermParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new ShelterTemperatureTerm(parameters.Int("bonusTenths", 0, 2000), parameters.Bool("onlyInPrecipitation"));
    }

    /// <inheritdoc />
    public int TenthsAt(ITemperatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_onlyInPrecipitation && Permille.Of(context.Atmosphere.Channels.PrecipitationIntensity) == 0)
        {
            return 0;
        }

        return Permille.Times(Permille.Times(_bonus, Permille.Of(context.CoveredShare)), context.Exposure);
    }
}
