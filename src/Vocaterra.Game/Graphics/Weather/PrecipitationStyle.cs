namespace Vocaterra.Game.Graphics.Weather;

/// <summary>
/// How one kind of precipitation looks (E06): plain numbers, no engine type. A kind is a row of <see cref="PrecipitationStyles.Table"/>; the weather's channel names the row by its
/// <c>precipitation.kind</c> string, so a later kind (snow) is one more row and no drawing code switches on an id.
/// </summary>
/// <param name="Id">The precipitation kind the weather data names (the channel's <c>PrecipitationKind</c>).</param>
/// <param name="Length">How long a drop's streak is, in blocks (a fast drop is a streak; a slow flake would be short).</param>
/// <param name="Width">How wide the streak is, in blocks.</param>
/// <param name="FallSpeed">How fast a drop falls, in blocks a second.</param>
/// <param name="Colour">The streak's colour in full light (the look's sun light dims it).</param>
/// <param name="Opacity">How opaque the head of a streak is, 0 to 1; its tail fades out.</param>
/// <param name="WindSlant">How far the wind slants a drop at the full wind channel: blocks of drift sideways for each block fallen.</param>
/// <param name="CountShare">The share of the quality's drop budget this kind uses at full intensity, 0 to 1.</param>
public sealed record PrecipitationStyle(
    string Id,
    float Length,
    float Width,
    float FallSpeed,
    (byte R, byte G, byte B) Colour,
    float Opacity,
    float WindSlant,
    float CountShare);
