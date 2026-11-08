namespace Vocaterra.Game.Core.Water;

/// <summary>What one tick of <see cref="Breath"/> reports. It states what happened and decides no consequence.</summary>
[Flags]
public enum BreathEvents
{
    /// <summary>Nothing worth reporting.</summary>
    None = 0,

    /// <summary>The air is at zero under water: raised the tick it reaches zero and again every <see cref="Breath.OutOfAirIntervalTicks"/> ticks while it stays there.</summary>
    OutOfAir = 1,

    /// <summary>The air became full again after having been below full.</summary>
    Restored = 2,
}
