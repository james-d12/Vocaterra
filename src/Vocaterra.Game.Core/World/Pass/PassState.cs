namespace Vocaterra.Game.Core.World.Pass;

/// <summary>
/// Whether the pass through the east ring is open (B08b, D-game-34 items 6 and 15). It opens once every lesson of <see cref="PassData.RequiredLessons"/> is complete, or at once
/// under the cheat setting, and then it never closes again: the cheat turned off, a save and a load leave it open (the save keeps <see cref="IsOpen"/>, and the rockfall's removal
/// is in the world's changed chunks). The rule is <see cref="ShouldBeOpen"/>, a pure query over the learner's completed lessons; no lesson id is in code. B07 lifts the
/// valley's edge wall when <see cref="IsOpen"/> is true (or when <see cref="Opened"/> is raised).
/// </summary>
public sealed class PassState
{
    private readonly PassData _data;

    /// <summary>Creates the lock, closed (open when the data requires no lesson: a world without a pass section has no lock).</summary>
    /// <param name="data">The pass's data.</param>
    public PassState(PassData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        IsOpen = !Locked;
    }

    /// <summary>Raised once, when the pass opens in play (never by <see cref="Restore"/>, whose blocks are already in the world).</summary>
    public event Action? Opened;

    /// <summary>The pass's data.</summary>
    public PassData Data => _data;

    /// <summary>Whether there is a lock at all: the world's file lists at least one required lesson.</summary>
    public bool Locked => _data.RequiredLessons.Count > 0;

    /// <summary>Whether the pass is open now. Once true it stays true.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// The rule: the pass is open when the cheat is on, or every required lesson is complete. A pure query: it reads nothing but its arguments and does not remember the answer
    /// (that is <see cref="Update"/>).
    /// </summary>
    /// <param name="data">The pass's data.</param>
    /// <param name="isDone">Whether a lesson is complete (<c>LessonRunner.IsDone</c>).</param>
    /// <param name="cheat">The cheat setting.</param>
    public static bool ShouldBeOpen(PassData data, Func<string, bool> isDone, bool cheat)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(isDone);
        return data.RequiredLessons.Count == 0 || cheat || data.RequiredLessons.All(isDone);
    }

    /// <summary>Opens the pass when <see cref="ShouldBeOpen"/> says so and it is closed: sets <see cref="IsOpen"/> and raises <see cref="Opened"/>.</summary>
    /// <param name="isDone">Whether a lesson is complete.</param>
    /// <param name="cheat">The cheat setting.</param>
    /// <returns>Whether the pass opened just now.</returns>
    public bool Update(Func<string, bool> isDone, bool cheat)
    {
        if (IsOpen || !ShouldBeOpen(_data, isDone, cheat))
        {
            return false;
        }

        IsOpen = true;
        Opened?.Invoke();
        return true;
    }

    /// <summary>Puts the state back as a save had it: open stays open without the event (the rockfall's blocks are already gone in the restored world).</summary>
    /// <param name="open">Whether the save says the pass was open.</param>
    public void Restore(bool open) => IsOpen = open || !Locked;
}
