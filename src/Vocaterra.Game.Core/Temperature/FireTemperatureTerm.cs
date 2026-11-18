namespace Vocaterra.Game.Core.Temperature;

/// <summary>A fire's warmth (I06): a bonus while the player is within the radius of a lit fire ring (<see cref="Needs.FireRings.NearLit"/>). From <c>game/temperature.json</c>.</summary>
public sealed class FireTemperatureTerm : ITemperatureTerm
{
    /// <summary>The term's id in the data and the registry.</summary>
    public const string Id = "fire";

    private readonly int _radius;
    private readonly int _bonus;

    /// <summary>A fire term.</summary>
    /// <param name="radiusBlocks">How far from the ring's middle the fire warms, in blocks across.</param>
    /// <param name="bonusTenths">The warmth, in tenths of a degree.</param>
    public FireTemperatureTerm(int radiusBlocks, int bonusTenths)
    {
        _radius = radiusBlocks;
        _bonus = bonusTenths;
    }

    /// <inheritdoc />
    public string Name => Id;

    /// <summary>Reads the term's row.</summary>
    /// <param name="parameters">The row's parameters.</param>
    public static ITemperatureTerm From(TermParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new FireTemperatureTerm(parameters.Int("radiusBlocks", 1, 64), parameters.Int("bonusTenths", 0, 2000));
    }

    /// <inheritdoc />
    public int TenthsAt(ITemperatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Fires is { } fires && fires.NearLit(context.Feet, _radius) ? _bonus : 0;
    }
}
