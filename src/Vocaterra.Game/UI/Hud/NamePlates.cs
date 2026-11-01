using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Figures;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// Which NPCs show their name plate (V22, D-game-22): the one the player faces within talking range (<see cref="FigureDirector.Aimed"/>, the NPC E talks to), and
/// the one the beacon points at, no other. A plate used to hang over every figure in view, and showed through the shopkeeper's face and the farmer's.
/// </summary>
public static class NamePlates
{
    /// <summary>
    /// The figures whose plates are drawn: the faced one first, then the beacon's, one plate when they are the same NPC. The beacon points at an NPC when it stands on the
    /// block of the NPC's feet (the session's arrow to an NPC, <c>PointAtNpc</c>).
    /// </summary>
    /// <param name="figures">All the NPC figures.</param>
    /// <param name="faced">The figure the player faces within talking range, or <see langword="null"/>.</param>
    /// <param name="beaconTarget">The block the beacon stands on, or <see langword="null"/> when there is none.</param>
    /// <param name="speaker">The scene's name of the NPC in the open conversation, whose name is on the bubble instead (S15), or <see langword="null"/>.</param>
    public static IReadOnlyList<NpcFigure> Select(IReadOnlyList<NpcFigure> figures, NpcFigure? faced, BlockPos? beaconTarget, string? speaker)
    {
        var shown = new List<NpcFigure>(2);
        if (faced is not null)
        {
            shown.Add(faced);
        }

        if (beaconTarget is { } target && figures.FirstOrDefault(figure => figure.Feet == target) is { } pointedAt && !shown.Contains(pointedAt))
        {
            shown.Add(pointedAt);
        }

        shown.RemoveAll(figure => string.Equals(figure.Name, speaker, StringComparison.Ordinal));
        return shown;
    }
}
