using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// Which way a figure faces (S15): the NPCs stand where the scene put them and turn to face the player when spoken to, and turn back to their own facing
/// when the conversation is over. Plain maths in angle steps (0 north, 1 024 east, as <see cref="Angles"/>), so the rule is tested without a window.
/// </summary>
public static class FigureFacing
{
    /// <summary>The most a figure turns in one tick, in angle steps (a half turn takes about 0.4 seconds).</summary>
    public const double MaxStepPerTick = 80.0;

    /// <summary>The facing, in angle steps, of someone at (<paramref name="fromX"/>, <paramref name="fromZ"/>) looking at (<paramref name="toX"/>, <paramref name="toZ"/>): 0 when the target is to the north, 1 024 to the east.</summary>
    public static double TowardSteps(double fromX, double fromZ, double toX, double toZ)
    {
        var dx = toX - fromX;
        var dz = toZ - fromZ;
        if (dx == 0 && dz == 0)
        {
            return 0;
        }

        var turns = Math.Atan2(dx, -dz) / Math.Tau; // north is -z: atan2(east, north)
        var steps = turns * Angles.StepsPerTurn;
        return steps < 0 ? steps + Angles.StepsPerTurn : steps;
    }

    /// <summary>The shortest signed turn in angle steps (-2 048 to 2 048) from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static double Difference(double from, double to)
    {
        var d = (to - from) % Angles.StepsPerTurn;
        if (d > Angles.StepsPerTurn / 2.0)
        {
            d -= Angles.StepsPerTurn;
        }
        else if (d < -Angles.StepsPerTurn / 2.0)
        {
            d += Angles.StepsPerTurn;
        }

        return d;
    }

    /// <summary>The facing after one tick of turning from <paramref name="current"/> towards <paramref name="target"/> by the shorter way, by at most <paramref name="maxStep"/> steps; wrapped into [0, 4 096).</summary>
    public static double Step(double current, double target, double maxStep = MaxStepPerTick)
    {
        var d = Difference(current, target);
        var next = Math.Abs(d) <= maxStep ? target : current + (Math.Sign(d) * maxStep);
        next %= Angles.StepsPerTurn;
        return next < 0 ? next + Angles.StepsPerTurn : next;
    }

    /// <summary>
    /// The facing a figure wants now: towards the player while it is in a conversation, otherwise the facing the scene gave it.
    /// </summary>
    /// <param name="figure">The figure.</param>
    /// <param name="playerX">The player's east position in blocks.</param>
    /// <param name="playerZ">The player's south position in blocks.</param>
    public static double Wanted(NpcFigure figure, double playerX, double playerZ)
    {
        ArgumentNullException.ThrowIfNull(figure);
        return figure.Talking ? TowardSteps(figure.Feet.X + 0.5, figure.Feet.Z + 0.5, playerX, playerZ) : figure.Yaw;
    }

    /// <summary>Turns a figure one tick towards the facing it wants (see <see cref="Wanted"/>).</summary>
    public static void Update(NpcFigure figure, double playerX, double playerZ)
    {
        ArgumentNullException.ThrowIfNull(figure);
        figure.FacingSteps = Step(figure.FacingSteps, Wanted(figure, playerX, playerZ));
    }
}
