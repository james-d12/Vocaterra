namespace Vocaterra.Game.Core.Needs;

/// <summary>
/// What the needs do to the player right now, worked out from the meters and the data and never stored (design 9.10): stamina's maximum and refill, the walking
/// factor, whether the player may sprint, and how health moves.
/// </summary>
/// <param name="StaminaMaximum">100 minus the three drives' penalties, never below the data's minimum.</param>
/// <param name="RefillFactor">The product of the drives' refill factors, never below the data's minimum (a quarter). Rest is not in it.</param>
/// <param name="StaminaRefillPerSecond">How fast stamina refills once the player has been free of effort for the delay: the speed times the factor, times rest's factor when resting.</param>
/// <param name="WalkFactor">The slowest walking factor in force (they never multiply); 1 when none applies.</param>
/// <param name="SprintAllowed">False at any drive's last stage and at very weak health. Stamina at 0 is separate (<see cref="PlayerNeeds.CanSprint"/>).</param>
/// <param name="HealthLossPerSecond">The losses of the drives at their last stage, added.</param>
/// <param name="RecoveryBlocked">Whether a drive at stage 3 or 4 stops health's recovery.</param>
/// <param name="HealthRecoveryPerSecond">Health's recovery: zero while blocked or while health is being lost, else a point every few seconds, times rest's factor when resting.</param>
/// <param name="Views">The names of the view effects in force (dimmed edges, a blink, a heart pulse, red edges), without repeats.</param>
public sealed record NeedEffects(
    double StaminaMaximum,
    double RefillFactor,
    double StaminaRefillPerSecond,
    double WalkFactor,
    bool SprintAllowed,
    double HealthLossPerSecond,
    bool RecoveryBlocked,
    double HealthRecoveryPerSecond,
    IReadOnlyList<string> Views)
{
    /// <summary>Health's change a second: its recovery minus its loss.</summary>
    public double HealthChangePerSecond => HealthRecoveryPerSecond - HealthLossPerSecond;
}

/// <summary>The needs as a save keeps them (<c>systems.needs</c>, save version 6): the five meters and the two timers.</summary>
/// <param name="Hunger">0 to 100.</param>
/// <param name="Thirst">0 to 100.</param>
/// <param name="Sleep">0 to 100.</param>
/// <param name="Stamina">0 to its maximum.</param>
/// <param name="Health">0 to 100.</param>
/// <param name="ExhaustionSeconds">Seconds of active play spent at sleep's last stage (a collapse comes at the data's delay).</param>
/// <param name="EffortFreeSeconds">Seconds since the last effort (stamina refills once this passes the data's delay).</param>
public sealed record NeedsSave(double Hunger, double Thirst, double Sleep, double Stamina, double Health, double ExhaustionSeconds = 0, double EffortFreeSeconds = 0);

/// <summary>
/// The player's needs (N01, adaptation design section 9): plain state for the five meters (hunger, thirst and sleep, 0 to 100; stamina, 0 to its current maximum; health,
/// 0 to 100) and the exhaustion timer. Everything the meters do is worked out from them and the data and never stored: <see cref="StageOf"/>, <see cref="HealthStage"/> and
/// <see cref="Effects"/>. No clock and no random numbers: <see cref="Step"/> is told how many world ticks of active play passed and what the player was doing, and the host
/// decides which ticks are active play (the session is told each tick; see <see cref="PlayPause"/>).
/// </summary>
/// <remarks>
/// A stage is read from the meter after every change: healthy from the first point up, stage 1 below 75, stage 2 below 50, stage 3 below 25 and stage 4 at 0 (the points are
/// the data's), so a recovery to exactly 25, 50 or 75 takes the worse stage off. Penalties add, refill factors multiply (never below a quarter), the slowest walking factor
/// applies, health losses add and health recovers only while no drive is at stage 3 or 4. Eating, drinking, a night, a collapse, a death and waking change the meters once.
/// </remarks>
public sealed class PlayerNeeds
{
    /// <summary>The top of a drive and of health.</summary>
    public const double Full = 100;

    private readonly NeedsData _data;
    private double _hunger = Full;
    private double _thirst = Full;
    private double _sleep = Full;
    private double _stamina;
    private double _health = Full;
    private double _exhaustion;
    private double _effortFree;

    /// <summary>Full meters.</summary>
    /// <param name="data">The needs' data.</param>
    public PlayerNeeds(NeedsData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _stamina = data.Stamina.Maximum;
        _effortFree = data.Stamina.RefillDelaySeconds;
    }

    /// <summary>The data the needs run on.</summary>
    public NeedsData Data => _data;

    /// <summary>The value of a meter.</summary>
    /// <param name="meter">Which meter.</param>
    public double Value(NeedMeter meter) => meter switch
    {
        NeedMeter.Hunger => _hunger,
        NeedMeter.Thirst => _thirst,
        NeedMeter.Sleep => _sleep,
        NeedMeter.Stamina => _stamina,
        NeedMeter.Health => _health,
        _ => throw new ArgumentOutOfRangeException(nameof(meter), meter, "Not a meter."),
    };

    /// <summary>Seconds of active play spent at sleep's last stage.</summary>
    public double ExhaustionSeconds => _exhaustion;

    /// <summary>Seconds since the last effort.</summary>
    public double EffortFreeSeconds => _effortFree;

    /// <summary>Whether health has run out (the player dies; <see cref="Wake"/> brings them back).</summary>
    public bool IsDead => _health <= 0;

    /// <summary>Whether the sleep drive has been at its last stage for the collapse's delay.</summary>
    public bool CollapseDue => _exhaustion >= _data.CollapseDelaySeconds - 1e-6 && StageOf(Drive.Sleep) == 4;

    /// <summary>Whether the player may sprint now: no stage forbids it and stamina is left.</summary>
    public bool CanSprint => _stamina > 0 && Effects().SprintAllowed;

    /// <summary>
    /// The drive that ends a death by hunger or thirst (the one whose loss is the faster when both are at their last stage: thirst), or <see langword="null"/> when neither
    /// is at its last stage. The death's event names it (<c>hambre</c> or <c>sed</c>).
    /// </summary>
    public Drive? DeathCause =>
        StageOf(Drive.Thirst) == 4 ? Drive.Thirst : StageOf(Drive.Hunger) == 4 ? Drive.Hunger : null;

    /// <summary>A drive's stage, from its meter: 0 healthy, 1 to 3 below their points, 4 at its last point.</summary>
    /// <param name="drive">Which drive.</param>
    public int StageOf(Drive drive)
    {
        var value = Value(drive switch { Drive.Hunger => NeedMeter.Hunger, Drive.Thirst => NeedMeter.Thirst, _ => NeedMeter.Sleep });
        var stages = _data.Of(drive).Stages;
        var stage = 0;
        for (var i = 0; i < stages.Count; i++)
        {
            var last = i == stages.Count - 1;
            if (last ? value <= stages[i].Point : value < stages[i].Point)
            {
                stage = i + 1;
            }
        }

        return stage;
    }

    /// <summary>Health's warning stage, from its meter: 0 healthy, 1 weak (below 50), 2 very weak (below 25).</summary>
    public int HealthStage
    {
        get
        {
            var warnings = _data.Health.Warnings;
            var stage = 0;
            for (var i = 0; i < warnings.Count; i++)
            {
                if (_health < warnings[i].Point)
                {
                    stage = i + 1;
                }
            }

            return stage;
        }
    }

    /// <summary>What the needs do to the player now, from the meters and the data.</summary>
    /// <param name="resting">Whether the player stands still within the rest radius of a lit fire.</param>
    public NeedEffects Effects(bool resting = false)
    {
        var penalty = 0.0;
        var refill = 1.0;
        var walk = 1.0;
        var sprint = true;
        var loss = 0.0;
        var blocked = false;
        var views = new List<string>();
        foreach (var drive in new[] { Drive.Hunger, Drive.Thirst, Drive.Sleep })
        {
            var stage = StageOf(drive);
            if (stage == 0)
            {
                continue;
            }

            var e = _data.Of(drive).Stages[stage - 1].Effects;
            penalty += e.StaminaPenalty;
            refill *= e.RefillFactor;
            walk = Math.Min(walk, e.WalkFactor);
            sprint &= e.SprintAllowed;
            loss += e.HealthLossEverySeconds > 0 ? e.HealthLoss / e.HealthLossEverySeconds : 0;
            blocked |= e.BlocksRecovery;
            AddView(views, e.View);
        }

        if (HealthStage is var h and > 0)
        {
            var e = _data.Health.Warnings[h - 1].Effects;
            walk = Math.Min(walk, e.WalkFactor);
            sprint &= e.SprintAllowed;
            AddView(views, e.View);
        }

        var stamina = _data.Stamina;
        var maximum = Math.Max(stamina.MinimumMaximum, stamina.Maximum - penalty);
        refill = Math.Max(stamina.MinimumRefillFactor, refill);
        var recovery = blocked || loss > 0 ? 0 : (1 / _data.Health.RecoverySeconds) * (resting ? _data.Rest.RecoveryFactor : 1);
        return new NeedEffects(maximum, refill, stamina.RefillPerSecond * refill * (resting ? _data.Rest.RefillFactor : 1), walk, sprint, loss, blocked, recovery, views);
    }

    /// <summary>
    /// Moves the needs on through ticks of active play (the caller passes only those): the drives fall at their rate and the effort's factor, health is lost or recovers,
    /// stamina is spent or refills, and the exhaustion timer runs while sleep is at its last stage. A dead player's needs do not move. A <see cref="NeedActivity.Sprinting"/> the
    /// needs do not allow (<see cref="CanSprint"/>) counts as walking.
    /// </summary>
    /// <param name="ticks">How many world ticks.</param>
    /// <param name="activity">What the player did through them.</param>
    public void Step(int ticks, NeedActivity activity)
    {
        if (ticks <= 0 || IsDead)
        {
            return;
        }

        var seconds = ticks / (double)NeedsData.TicksPerSecond;
        var sprinting = activity == NeedActivity.Sprinting && CanSprint;
        var gathering = activity == NeedActivity.Gathering;
        _hunger = Fall(_hunger, _data.Of(Drive.Hunger), seconds, sprinting, gathering);
        _thirst = Fall(_thirst, _data.Of(Drive.Thirst), seconds, sprinting, gathering);
        _sleep = Fall(_sleep, _data.Of(Drive.Sleep), seconds, sprinting, gathering);

        var effects = Effects(activity == NeedActivity.Resting);
        _health = Math.Clamp(_health + (effects.HealthChangePerSecond * seconds), 0, Full);

        var stamina = _data.Stamina;
        if (sprinting)
        {
            _stamina -= stamina.SprintPerSecond * seconds;
        }
        else if (gathering)
        {
            _stamina -= stamina.GatherPerTick * ticks;
        }

        if (sprinting || gathering)
        {
            _effortFree = 0;
        }
        else
        {
            _effortFree += seconds;
            if (_effortFree >= stamina.RefillDelaySeconds)
            {
                _stamina += effects.StaminaRefillPerSecond * seconds;
            }
        }

        _stamina = Math.Clamp(_stamina, 0, effects.StaminaMaximum);
        _exhaustion = StageOf(Drive.Sleep) == 4 ? _exhaustion + seconds : 0;
    }

    /// <summary>
    /// Eats or drinks a food: its hunger, thirst and healing are added, each capped at 100, and the food is used up (the result is true). At full hunger a food that only gives
    /// hunger is kept (false), cooked or not; the same for a drink at full thirst. A dead player eats nothing. The water bottle is a food that gives thirst.
    /// </summary>
    /// <param name="food">What the food gives (<see cref="NeedsData.FoodOf"/>).</param>
    public bool Eat(NeedFood food)
    {
        ArgumentNullException.ThrowIfNull(food);
        if (IsDead || !((food.Hunger > 0 && _hunger < Full) || (food.Thirst > 0 && _thirst < Full)))
        {
            return false;
        }

        _hunger = Math.Min(Full, _hunger + food.Hunger);
        _thirst = Math.Min(Full, _thirst + food.Thirst);
        _health = Math.Min(Full, _health + food.Healing);
        return true;
    }

    /// <summary>A drink with no bottle (by hand at the river, the lake or the well): thirst rises by the data's amount, capped at 100. False at full thirst, and when dead.</summary>
    public bool Drink()
    {
        if (IsDead || _thirst >= Full)
        {
            return false;
        }

        _thirst = Math.Min(Full, _thirst + _data.DrinkByHandThirst);
        return true;
    }

    /// <summary>A night's sleep (design 9.5): sleep to 100, health up, hunger and thirst down without going under the floor, then stamina filled to its new maximum.</summary>
    public void Sleep() => ApplyChange(_data.Night);

    /// <summary>A collapse from exhaustion: sleep to 50, no health, the same costs, stamina filled. It changes the meters once and resets the exhaustion timer.</summary>
    public void Collapse() => ApplyChange(_data.Collapse);

    /// <summary>Health runs out: the player is dead until <see cref="Wake"/>.</summary>
    public void Die() => _health = 0;

    /// <summary>Wakes after a death (design 9.7): health and the three drives at the data's waking values, stamina at its effective maximum, no exhaustion.</summary>
    public void Wake()
    {
        var death = _data.Death;
        _health = death.Health;
        _hunger = death.Hunger;
        _thirst = death.Thirst;
        _sleep = death.Sleep;
        _exhaustion = 0;
        _effortFree = _data.Stamina.RefillDelaySeconds;
        _stamina = Effects().StaminaMaximum;
    }

    /// <summary>Sets one meter to a value, clamped to its range (a drive or health 0 to 100, stamina 0 to its maximum). The test aid <c>--set-need</c> and the load use it.</summary>
    /// <param name="meter">Which meter.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a finite number.</exception>
    public void Set(NeedMeter meter, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A need's value must be a finite number.");
        }

        var clamped = Math.Clamp(value, 0, Full);
        switch (meter)
        {
            case NeedMeter.Hunger:
                _hunger = clamped;
                break;
            case NeedMeter.Thirst:
                _thirst = clamped;
                break;
            case NeedMeter.Sleep:
                _sleep = clamped;
                break;
            case NeedMeter.Health:
                _health = clamped;
                break;
            default:
                _stamina = Math.Clamp(value, 0, Effects().StaminaMaximum);
                break;
        }

        _stamina = Math.Min(_stamina, Effects().StaminaMaximum);
    }

    /// <summary>The needs as a save keeps them.</summary>
    public NeedsSave Capture() => new(_hunger, _thirst, _sleep, _stamina, _health, _exhaustion, _effortFree);

    /// <summary>
    /// Puts saved needs back exactly. A value out of its range is clamped into it (a drive or health to 0 to 100, stamina to its maximum, a timer to 0 or more); a value that is not
    /// a finite number refuses the save.
    /// </summary>
    /// <param name="save">The save's entry.</param>
    /// <exception cref="ArgumentException">A value is not a finite number.</exception>
    public void Restore(NeedsSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (!new[] { save.Hunger, save.Thirst, save.Sleep, save.Stamina, save.Health, save.ExhaustionSeconds, save.EffortFreeSeconds }.All(double.IsFinite))
        {
            throw new ArgumentException("The saved needs hold a value that is not a number.", nameof(save));
        }

        _hunger = Math.Clamp(save.Hunger, 0, Full);
        _thirst = Math.Clamp(save.Thirst, 0, Full);
        _sleep = Math.Clamp(save.Sleep, 0, Full);
        _health = Math.Clamp(save.Health, 0, Full);
        _exhaustion = Math.Max(0, save.ExhaustionSeconds);
        _effortFree = Math.Max(0, save.EffortFreeSeconds);
        _stamina = Math.Clamp(save.Stamina, 0, Effects().StaminaMaximum);
    }

    private static double Fall(double value, DriveSpec spec, double seconds, bool sprinting, bool gathering) =>
        Math.Max(0, value - (seconds / spec.SecondsPerPoint * (sprinting ? spec.SprintFactor : gathering ? spec.GatherFactor : 1)));

    private void ApplyChange(SleepChange change)
    {
        _sleep = change.SleepTo;
        _health = Math.Min(Full, _health + change.Health);
        _hunger = Lower(_hunger, change.HungerCost, change.Floor);
        _thirst = Lower(_thirst, change.ThirstCost, change.Floor);
        _exhaustion = 0;
        _effortFree = _data.Stamina.RefillDelaySeconds;
        _stamina = Effects().StaminaMaximum;
    }

    // Lowered by a cost but never under the floor; a meter already under the floor stays where it is.
    private static double Lower(double value, double cost, double floor) => value < floor ? value : Math.Max(floor, value - cost);

    private static void AddView(List<string> views, string? view)
    {
        if (view is not null && !views.Contains(view, StringComparer.Ordinal))
        {
            views.Add(view);
        }
    }
}
