namespace Vocaterra.Game.Graphics.Weather;

/// <summary>One drop as the line it draws (E06): the head (the lower end, where the drop is), the tail (up the line it fell along) and how opaque the head is. Plain floats: the renderer makes the quad.</summary>
/// <param name="HeadX">The head's east-west position, in blocks.</param>
/// <param name="HeadY">The head's height.</param>
/// <param name="HeadZ">The head's north-south position.</param>
/// <param name="TailX">The tail's east-west position.</param>
/// <param name="TailY">The tail's height.</param>
/// <param name="TailZ">The tail's north-south position.</param>
/// <param name="Alpha">The head's opacity, 0 to 1.</param>
public readonly record struct Streak(float HeadX, float HeadY, float HeadZ, float TailX, float TailY, float TailZ, float Alpha);
