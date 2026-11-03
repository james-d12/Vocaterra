using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// One step of an objective: its key, the word the checklist shows, the noun it is about, how many it needs and how many the player has done.
/// </summary>
/// <param name="Key">The step's id inside its objective (<c>wood</c>, <c>berry</c>, <c>torch</c>, <c>water</c>), ordinal, never shown.</param>
/// <param name="Label">The word the checklist shows (<c>madera</c>, <c>frutas</c>, <c>antorcha</c>, <c>agua</c>).</param>
/// <param name="Noun">The catalogue noun the step is about (<c>madera</c>, <c>fruta</c>): its support-language word glosses the step until the noun is known. Empty for a step of an objective that is no checklist.</param>
/// <param name="Target">How many make the step done (positive).</param>
/// <param name="Count">How many the player has done so far (it may pass the target; the step shows the target at most).</param>
public sealed record ObjectiveStep(string Key, string Label, string Noun, int Target, int Count)
{
    /// <summary>Whether the step is done.</summary>
    public bool Done => Count >= Target;

    /// <summary>The step as the checklist writes it: <c>madera 5/5</c>.</summary>
    public string Text => $"{Label} {Math.Min(Count, Target)}/{Target}";

    /// <summary>The step with <paramref name="by"/> more done.</summary>
    public ObjectiveStep Advanced(int by) => this with { Count = Count + by };
}

/// <summary>
/// One objective of the session, as <c>game/objectives/&lt;id&gt;.json</c> gives it (C07; before it the camp checklist was code): steps that count the world events
/// their triggers match. A checklist (the camp, <c>Campamento</c>) shows its steps; the others show one line. Steps move only on world events (pickups, the placed
/// torch, the filled bottle), never on the learner's language (implementation-plan rule 8). Plain values, no clock: the same events give the same checklist.
/// </summary>
public sealed class Objective
{
    private readonly ObjectiveStep[] _steps;

    /// <summary>Creates the objective with nothing done.</summary>
    /// <param name="spec">The objective's data.</param>
    public Objective(ObjectiveSpec spec)
    {
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _steps = spec.Steps.Select(static s => new ObjectiveStep(s.Id, s.Label, s.Noun, s.Target, 0)).ToArray();
    }

    /// <summary>The objective's data.</summary>
    public ObjectiveSpec Spec { get; }

    /// <summary>The objective's id (<c>camp</c>).</summary>
    public string Id => Spec.Id;

    /// <summary>The checklist's title, shown before the steps (<c>Campamento</c>); empty for an objective that is no checklist.</summary>
    public string Title => Spec.Title ?? string.Empty;

    /// <summary>Whether the HUD shows the objective as a checklist of its steps.</summary>
    public bool IsChecklist => Spec.IsChecklist;

    /// <summary>The steps in checklist order.</summary>
    public IReadOnlyList<ObjectiveStep> Steps => _steps;

    /// <summary>Whether every step is done.</summary>
    public bool Complete => _steps.All(static s => s.Done);

    /// <summary>Whether any step has moved.</summary>
    public bool Started => _steps.Any(static s => s.Count > 0);

    /// <summary>The checklist as one line: <c>Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1</c>.</summary>
    public string Summary => Title + ": " + string.Join(" · ", _steps.Select(static s => s.Text));

    /// <summary>
    /// Sets the count of each step by its key (S10: a save puts the checklist back). Each key must be a step of the objective and appear once; the steps not named
    /// keep their count.
    /// </summary>
    /// <param name="counts">The counts by step key.</param>
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
                throw new ArgumentException($"'{key}' is not a step of the objective '{Id}', or is given twice.", nameof(counts));
            }

            _steps[at] = _steps[at] with { Count = count };
        }
    }

    /// <summary>The step with a key, or <see langword="null"/>.</summary>
    /// <param name="key">The step's id.</param>
    public ObjectiveStep? Step(string key) => _steps.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    /// <summary>Counts a world event: every step whose trigger matches it moves by <paramref name="amount"/> (a pickup's items).</summary>
    /// <param name="world">The world event.</param>
    /// <param name="amount">How many the event counts for.</param>
    /// <returns>The steps this call completed (empty when none).</returns>
    public IReadOnlyList<ObjectiveStep> Record(WorldEvent world, int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var completed = new List<ObjectiveStep>();
        for (var i = 0; i < _steps.Length; i++)
        {
            if (!Spec.Steps[i].Trigger.Matches(world))
            {
                continue;
            }

            var before = _steps[i];
            _steps[i] = before.Advanced(amount);
            if (!before.Done && _steps[i].Done)
            {
                completed.Add(_steps[i]);
            }
        }

        return completed;
    }

    /// <summary>Counts a world event by its name and detail (an event that is not logged: a test, or the host's shortcut).</summary>
    /// <param name="worldEvent">The event's name (<see cref="WorldEvents"/>).</param>
    /// <param name="detail">Its detail (<c>madera</c>), or <see langword="null"/>.</param>
    /// <param name="amount">How many the event counts for.</param>
    /// <returns>The steps this call completed (empty when none).</returns>
    public IReadOnlyList<ObjectiveStep> Record(string worldEvent, string? detail = null, int amount = 1) => Record(new WorldEvent(0, 0, worldEvent, null, detail), amount);

    // Completes every step (an objective another one's progress completes, alsoDoneBy).
    internal void Finish()
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            _steps[i] = _steps[i] with { Count = Math.Max(_steps[i].Count, _steps[i].Target) };
        }
    }

    // Sets one step's count (a restore hook).
    internal void Set(string key, int count)
    {
        var at = Array.FindIndex(_steps, s => string.Equals(s.Key, key, StringComparison.Ordinal));
        _steps[at] = _steps[at] with { Count = count };
    }
}
