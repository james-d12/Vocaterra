namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// What shows of the gear on the player (I09): one colour for each body part something covers, the item on the back and the item in the off-hand. Plain values the front-end turns into boxes and models;
/// <see cref="AppearanceBuilder"/> makes it from the equipment.
/// </summary>
/// <param name="Parts">The colour of each covered part by open part key (<c>head</c>, <c>body</c>, <c>legs</c>, <c>feet</c>...), the top layer's. A part nothing covers is absent.</param>
/// <param name="Sleeve">The colour of the sleeves (the arms): the <c>sleeve</c> part or, without one, the body's; <see langword="null"/> when nothing covers the body.</param>
/// <param name="Back">The item worn on the back (a bag), or <see langword="null"/>.</param>
/// <param name="OffHand">The item in the off-hand, or <see langword="null"/>.</param>
public sealed record Appearance(IReadOnlyDictionary<string, LookColour> Parts, LookColour? Sleeve, ItemDefinition? Back, ItemDefinition? OffHand)
{
    /// <summary>Nothing shows: the player looks as without gear.</summary>
    public static Appearance None { get; } = new(new Dictionary<string, LookColour>(), null, null, null);

    /// <summary>Whether nothing of the gear shows.</summary>
    public bool IsNone => Parts.Count == 0 && Sleeve is null && Back is null && OffHand is null;
}
