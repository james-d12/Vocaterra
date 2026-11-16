namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The player's own warmth in the temperature's units (I06): what clothes and the like add to how the cold feels, in tenths of a degree. The feeling is the place's temperature
/// plus this. Until the equipment's stats exist (I03a) the only source is <see cref="NoWarmth"/>; the equipment's <c>warmth</c> stat becomes another implementation, set on the
/// session, and nothing else changes.
/// </summary>
public interface IWarmthSource
{
    /// <summary>The warmth now, in tenths of a degree (0 for none; a negative number would make the cold feel worse).</summary>
    int WarmthTenths();
}
