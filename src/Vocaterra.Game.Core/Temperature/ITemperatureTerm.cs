namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// One contribution to the temperature of a place at a moment (I06): the valley's base, the day's swing, the weather's cooling, a roof's shelter, a fire's warmth. A term reads
/// the <see cref="TemperatureSituation"/> and answers in tenths of a degree Celsius (125 is 12.5 degrees), as an integer, with the same answer for the same situation on every
/// machine. <see cref="PlaceTemperature"/> sums the terms it is given and knows none of them, so a new cause of warmth or cold is one new class and its registration.
/// </summary>
public interface ITemperatureTerm
{
    /// <summary>A short name for the term (<c>day</c>, <c>fire</c>): what a breakdown lists it as. Not a decision for any code.</summary>
    string Name { get; }

    /// <summary>What the term adds (positive, warmer) or takes away (negative, colder) here and now, in tenths of a degree.</summary>
    /// <param name="situation">The place and the moment.</param>
    int TenthsAt(in TemperatureSituation situation);
}
