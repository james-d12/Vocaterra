namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The fog of the world as plain numbers: its colour (each channel 0 to 1) and the distances, in blocks, where it starts and where it is complete.
/// No graphics types: the renderer turns it into its effects' settings.
/// </summary>
/// <param name="Red">The red channel, 0 to 1.</param>
/// <param name="Green">The green channel, 0 to 1.</param>
/// <param name="Blue">The blue channel, 0 to 1.</param>
/// <param name="Start">The distance in blocks where the fog starts.</param>
/// <param name="End">The distance in blocks where the fog is complete.</param>
public readonly record struct FogParameters(float Red, float Green, float Blue, float Start, float End);
