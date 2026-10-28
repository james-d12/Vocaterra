using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Dialogue;

/// <summary>
/// The guide greets the player who comes up to her (S17) and her lesson begins: the same opening the E key makes, once the world is free (no
/// script, no screen, no turn on the worker), and again only after the player has walked away (<see cref="GuideApproach"/>).
/// </summary>
public sealed class GuideGreeting(VoxelSession session, Action<string> openTalk, Action<string> console)
{
    private bool _armed = true;

    /// <summary>Once a frame: the greeting, when the player has come up to the guide and the world is <paramref name="free"/>.</summary>
    public void Update(bool free)
    {
        if (GuideApproach.ShouldGreet(session, ref _armed, free))
        {
            console("approach: the guide greets the player and her lesson begins");
            openTalk(NpcService.GuideAgent);
        }
    }
}
