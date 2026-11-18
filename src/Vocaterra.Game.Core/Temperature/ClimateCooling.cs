namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The numbers of the climate term (I06b, <c>game/temperature.json</c> <c>climate</c>): how the world's own climate temperature (the biomes' field, lowered by the lapse over height) cools the
/// player's place. The climate temperature is the generator's fixed-point value (4 096 is the top of the field's range, written 1 000 in thousandths, as <c>world.json</c> writes it).
/// </summary>
/// <param name="ReferenceThousandths">The climate temperature, in thousandths, at and above which the term is 0: the valley floor and the village sit above it, so they feel as before.</param>
/// <param name="CoolingTenthsPerRange">Tenths of a degree taken off for a climate temperature that many thousandths below the reference, per 1 000 thousandths (the field's whole range).</param>
/// <param name="MaxCoolingTenths">The most the term takes off, in tenths of a degree (the floor of the term).</param>
public readonly record struct ClimateCooling(int ReferenceThousandths, int CoolingTenthsPerRange, int MaxCoolingTenths);
