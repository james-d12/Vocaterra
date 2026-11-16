namespace Vocaterra.Game.Core.Temperature;

/// <summary>A fire's warmth (I06): a bonus while the player is within the radius of a lit fire ring (<see cref="Needs.FireRings.NearLit"/>). From <c>game/temperature.json</c>.</summary>
public sealed class FireTemperatureTerm : ITemperatureTerm
{
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
    public string Name => "fire";

    /// <inheritdoc />
    public int TenthsAt(in TemperatureSituation situation) => situation.Fires is { } fires && fires.NearLit(situation.Feet, _radius) ? _bonus : 0;
}
