namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// What the temperature keeps across a save (I06c, <c>systems.temperature</c>): how far the felt temperature has got towards the place's, in millionths of a tenth of a degree. The band it
/// falls in slows the walk and quickens the thirst, so the felt value is part of what makes the next tick: a load that took the place's value at once would continue differently. Left out
/// (starting at the place's value) when nothing lags (no lag in the data, or no reading yet). Not a version bump: a save without it starts at the place's value.
/// </summary>
/// <param name="Felt">The felt temperature, in millionths of a tenth of a degree.</param>
public sealed record TemperatureSave(long Felt);
