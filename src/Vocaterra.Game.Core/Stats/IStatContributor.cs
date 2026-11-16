namespace Vocaterra.Game.Core.Stats;

/// <summary>
/// Something that changes the player's stats (I03a): the worn equipment first, later a meal's buff (W15), a spell or a place's effect. Each tick the pipeline gives every contributor a
/// <see cref="StatBuilder"/> to add to; a new source is a new class and its registration, with no edit of the sheet or of anything that reads it.
/// </summary>
public interface IStatContributor
{
    /// <summary>Adds this source's sums and factors. It reads only simulation state (no clock, no random numbers), so every machine builds the same sheet.</summary>
    /// <param name="builder">The builder of this tick's sheet.</param>
    void Contribute(StatBuilder builder);
}
