namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The hook through which an NPC's line is played again (S7 leaves it, S9 fills it): <c>Repetir</c> and <c>Más despacio</c> call it with the line and
/// whether it should be slow. Until S9 pre-renders the NPCs' voices nothing plays; the panel only highlights the line.
/// </summary>
public interface ILinePlayer
{
    /// <summary>Plays the NPC's line again.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="line">The Spanish text of the line.</param>
    /// <param name="slow">Whether to play it slowly (the <c>.slow</c> file of S9).</param>
    void Play(string npc, string line, bool slow);

    /// <summary>
    /// Plays a line after what is playing instead of replacing it (S8: the phrase support's sounds follow the NPC's line, and the two key words follow
    /// each other). A player with no queue replaces, as <see cref="Play"/> does.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="line">The Spanish text of the line.</param>
    /// <param name="slow">Whether to play it slowly.</param>
    void Append(string npc, string line, bool slow) => Play(npc, line, slow);
}
