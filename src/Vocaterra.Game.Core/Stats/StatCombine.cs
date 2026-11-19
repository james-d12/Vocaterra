namespace Vocaterra.Game.Core.Stats;

/// <summary>How the items (and any other contributors) that touch one stat key combine (I05a): one rule per key, in <see cref="StatRules"/>.</summary>
public enum StatCombine
{
    /// <summary>The amounts are added (<see cref="StatBuilder.Add"/>) and the stat is read as <see cref="StatSheet.Sum"/>: a rating or a bonus in the stat's own unit.</summary>
    Sum = 0,

    /// <summary>The amounts are multiplied (<see cref="StatBuilder.Multiply"/>) and the stat is read as <see cref="StatSheet.Factor"/>: a factor on something that is already there; 1 leaves it alone.</summary>
    Factor = 1,
}
