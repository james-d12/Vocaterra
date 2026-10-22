using Vocaterra.Simulation.Items;

namespace Vocaterra.Game;

/// <summary>
/// One step of the camp objective: its key, the word the checklist shows, how many it needs and how many the player has done.
/// </summary>
/// <param name="Key">A stable key (<c>wood</c>, <c>berry</c>, <c>torch</c>, <c>water</c>), ordinal, never shown.</param>
/// <param name="Label">The word the checklist shows (<c>madera</c>, <c>frutas</c>, <c>antorcha</c>, <c>agua</c>).</param>
/// <param name="Target">How many make the step done (positive).</param>
/// <param name="Count">How many the player has done so far (it may pass the target; the step shows the target at most).</param>
public sealed record ObjectiveStep(string Key, string Label, int Target, int Count)
{
    /// <summary>Whether the step is done.</summary>
    public bool Done => Count >= Target;

    /// <summary>The step as the checklist writes it: <c>madera 5/5</c>.</summary>
    public string Text => $"{Label} {Math.Min(Count, Target)}/{Target}";

    /// <summary>The step with <paramref name="by"/> more done.</summary>
    public ObjectiveStep Advanced(int by) => this with { Count = Count + by };
}

/// <summary>
/// The camp objective of the prototype (<c>Campamento</c>): a checklist the player completes by gathering, by placing the torch at the fire
/// ring and, later, by filling the bottle (the host event <see cref="WaterEvent"/>). Steps move only on host events and inventory events
/// (pickups, the placed torch, a named host event), never on the learner's language (IMPLEMENTATION_PLAN rule 8). Plain values, no clock: the
/// same events give the same checklist.
/// </summary>
public sealed class Objective
{
    /// <summary>The checklist's title, shown before the steps.</summary>
    public const string Title = "Campamento";

    /// <summary>The host event that completes the water step. S6 raises it when the bottle is filled; nothing raises it yet.</summary>
    public const string WaterEvent = "objective.water";

    /// <summary>The key of the wood step.</summary>
    public const string WoodKey = "wood";

    /// <summary>The key of the berries step.</summary>
    public const string BerryKey = "berry";

    /// <summary>The key of the torch step (placed at the fire ring).</summary>
    public const string TorchKey = "torch";

    /// <summary>The key of the water step.</summary>
    public const string WaterKey = "water";

    private readonly ObjectiveStep[] _steps;

    private Objective(ObjectiveStep[] steps)
    {
        _steps = steps;
    }

    /// <summary>The camp objective as the prototype gives it: five wood, three berries, one torch at the fire ring, one water.</summary>
    public static Objective Camp() =>
        new(
        [
            new ObjectiveStep(WoodKey, "madera", 5, 0),
            new ObjectiveStep(BerryKey, "frutas", 3, 0),
            new ObjectiveStep(TorchKey, "antorcha", 1, 0),
            new ObjectiveStep(WaterKey, "agua", 1, 0),
        ]);

    /// <summary>The steps in checklist order.</summary>
    public IReadOnlyList<ObjectiveStep> Steps => _steps;

    /// <summary>Whether every step is done.</summary>
    public bool Complete => _steps.All(static s => s.Done);

    /// <summary>The checklist as one line: <c>Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1</c>.</summary>
    public string Summary => Title + ": " + string.Join(" · ", _steps.Select(static s => s.Text));

    /// <summary>
    /// Sets the count of each step by its key (S10: a save puts the checklist back). Each key must be a step of the checklist and appear once; the
    /// steps not named keep their count.
    /// </summary>
    /// <exception cref="ArgumentException">A key is not a step, or is given twice.</exception>
    public void Restore(IEnumerable<(string Key, int Count)> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, count) in counts)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            var at = Array.FindIndex(_steps, s => string.Equals(s.Key, key, StringComparison.Ordinal));
            if (at < 0 || !seen.Add(key))
            {
                throw new ArgumentException($"'{key}' is not a step of the checklist, or is given twice.", nameof(counts));
            }

            _steps[at] = _steps[at] with { Count = count };
        }
    }

    /// <summary>The step with a key, or <see langword="null"/>.</summary>
    public ObjectiveStep? Step(string key) => _steps.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// Counts items that entered the inventory by a pickup: wood and berries are steps of the checklist; other items do not move it.
    /// </summary>
    /// <returns>The steps this call completed (empty when none).</returns>
    public IReadOnlyList<ObjectiveStep> RecordPickup(ItemId item, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return item switch
        {
            ItemId.Wood => Advance(WoodKey, count),
            ItemId.Berry => Advance(BerryKey, count),
            _ => [],
        };
    }

    /// <summary>
    /// Counts a torch placed in the world: the torch step is done when the torch stands at the fire ring (the caller says whether the cell is
    /// the ring's).
    /// </summary>
    /// <returns>The steps this call completed (empty when none).</returns>
    public IReadOnlyList<ObjectiveStep> RecordTorchPlaced(bool atFireRing) => atFireRing ? Advance(TorchKey, 1) : [];

    /// <summary>A named host event (for example <see cref="WaterEvent"/>). Unknown names change nothing.</summary>
    /// <returns>The steps this call completed (empty when none).</returns>
    public IReadOnlyList<ObjectiveStep> RecordHostEvent(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Equals(name, WaterEvent, StringComparison.Ordinal) ? Advance(WaterKey, 1) : [];
    }

    // Adds to one step; returns the step as a list when this call moved it from open to done.
    private IReadOnlyList<ObjectiveStep> Advance(string key, int by)
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            if (!string.Equals(_steps[i].Key, key, StringComparison.Ordinal))
            {
                continue;
            }

            var before = _steps[i];
            var after = before.Advanced(by);
            _steps[i] = after;
            return before.Done || !after.Done ? [] : [after];
        }

        return [];
    }
}
