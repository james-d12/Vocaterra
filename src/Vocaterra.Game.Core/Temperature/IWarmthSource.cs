namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The player's own warmth in the temperature's units (I06): what clothes and the like add to how the cold feels, in tenths of a degree. The feeling is the place's temperature
/// plus this. The session's source is <see cref="StatWarmth"/>, the stat sheet's <c>warmth</c> (I03a: the worn equipment fills it); <see cref="NoWarmth"/> stays for a test that wants none.
/// </summary>
public interface IWarmthSource
{
    /// <summary>The warmth now, in tenths of a degree (0 for none; a negative number would make the cold feel worse).</summary>
    int WarmthTenths();
}
