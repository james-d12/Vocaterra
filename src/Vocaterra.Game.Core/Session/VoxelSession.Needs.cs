using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// The player's needs on the world tick (N01, adaptation design section 9): the session owns the PlayerNeeds and steps it only while play is active. The game tells the
// session each frame why play is not active (PlayPauses): the world's ticks go on under the dialogue, the book, the inventory and crafting, and the stored time counts them
// all, so the needs count active ticks themselves. What the needs do to the player (walking, sprint), eating, drinking, sleep, the collapse and death are wired by N02 to N04;
// here the needs are stepped, their stage changes are raised and the state is saved and restored.
public sealed partial class VoxelSession
{
    // The stage of hunger, thirst, sleep and health last told (need_stage is raised when one changes).
    private int[]? _needStages;
    private PlayerNeeds? _needs;

    /// <summary>The player's needs: five meters and the exhaustion timer, with their stages and effects worked out from them (<see cref="PlayerNeeds"/>).</summary>
    public PlayerNeeds Needs => _needs ??= new PlayerNeeds(Data.Needs);

    /// <summary>
    /// Whether the needs run at all. Off (<c>--needs off</c>, a test aid) the meters stay where they are, full unless <c>--set-need</c> set one, so the language runs keep their
    /// baselines. On by default.
    /// </summary>
    public bool NeedsEnabled { get; set; } = true;

    /// <summary>
    /// The relaxed-needs setting (N05, design decision 17): every drive falls at half its rate, and at zero health the player faints and wakes at the camp with the bag kept and no chest.
    /// Off by default; a setting of the player (<c>--relaxed-needs</c>, kept in the save's settings), not the world's state: no meter, digest or replay holds it.
    /// </summary>
    public bool RelaxedNeeds
    {
        get => _relaxedNeeds;
        set
        {
            _relaxedNeeds = value;
            ApplyRateFactor();
        }
    }

    private bool _relaxedNeeds;
    private double _needsRate = SaveSettings.DefaultNeedsRate;

    /// <summary>
    /// The drive rate setting (N09): a multiplier on the fall of hunger, thirst and sleep, 0.5 to 4.0 in steps of 0.25, 1 by default; it multiplies with <see cref="RelaxedNeeds"/>.
    /// A setting of the player like the relaxed needs: no meter, digest or replay holds it (a save keeps the meters as they are). Set values are clamped and snapped.
    /// </summary>
    public double NeedsRate
    {
        get => _needsRate;
        set
        {
            _needsRate = SaveSettings.SnapNeedsRate(value);
            ApplyRateFactor();
        }
    }

    private void ApplyRateFactor() => Needs.RateFactor = _needsRate * (_relaxedNeeds ? RelaxedRateFactor : 1);

    /// <summary>The share of its rate a drive falls at under <see cref="RelaxedNeeds"/>.</summary>
    public const double RelaxedRateFactor = 0.5;

    /// <summary>
    /// Why play is not active now (<see cref="PlayPause"/>), set by the game each frame: no conversation, lesson panel, book, inventory, crafting, station, report, menu or check
    /// battery, and no speech being recorded or processed. <see cref="PlayPause.None"/> is active play, the only time the needs move.
    /// </summary>
    public PlayPause PlayPauses { get; set; }

    /// <summary>Whether play is active: nothing in <see cref="PlayPauses"/> is set.</summary>
    public bool PlayActive => PlayPauses == PlayPause.None;

    /// <summary>The stage of a need as last told: a drive's 0 to 4 or health's 0 to 2 (what <see cref="WorldEvents.NeedStage"/> last announced).</summary>
    /// <param name="meter">Hunger, thirst, sleep or health.</param>
    public int NeedStage(NeedMeter meter) => NeedStages()[StageSlot(meter)];

    // One tick of the needs, after the simulation stepped: the activity is what the tick showed (a gather tick, the body moved, standing still at the lit fire).
    private void StepNeeds(long fromX, long fromZ, bool sprinting)
    {
        if (!NeedsEnabled || !PlayActive)
        {
            return;
        }

        Needs.Step(1, ActivityOfTick(fromX, fromZ, sprinting));
        SyncNeedStages();
        DieIfDead(); // N04: at zero health the player is back at their fire
    }

    private NeedActivity ActivityOfTick(long fromX, long fromZ, bool sprinting)
    {
        var player = Simulation.Player;
        var moved = player.CenterX != fromX || player.CenterZ != fromZ;
        if (moved && sprinting)
        {
            return NeedActivity.Sprinting;
        }

        // Filling the bottle at the water is no labour: only blocks gathered by hand or with the axe cost stamina.
        if (Simulation.Events.Any(e => e.Kind == SimulationEventKind.GatherProgress && Simulation.Grid.GetBlock(e.X, e.Y, e.Z) != BlockId.Water))
        {
            return NeedActivity.Gathering;
        }

        if (moved)
        {
            return NeedActivity.Walking;
        }

        return NearLitFire(Data.Needs.Rest.RadiusBlocks) ? NeedActivity.Resting : NeedActivity.Idle;
    }

    // Standing within blocks across of a lit fire ring: the player's own once its torch is on the rim, the guide's camp ring likewise, the forge always (FireRings).
    private bool NearLitFire(int blocks) => Fires.NearLit(Simulation.Player.FeetBlock, blocks);

    /// <summary>
    /// Reads the stages of the five meters and raises <see cref="WorldEvents.NeedStage"/> for each that changed since it was last told, in the order hunger, thirst, sleep, health
    /// (the detail is the need's name from the data). Called after every tick of the needs and after anything else that changes a meter (eating, drinking, a night).
    /// </summary>
    public void SyncNeedStages()
    {
        var stages = NeedStages();
        foreach (var meter in new[] { NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep, NeedMeter.Health })
        {
            var now = StageNow(meter);
            if (now != stages[StageSlot(meter)])
            {
                stages[StageSlot(meter)] = now;
                LogWorld(WorldEvents.NeedStage, null, DetailOf(meter));
            }
        }
    }

    // The stages are taken as they are, with no event: a load, a new session, a test aid's value.
    internal void SettleNeedStages()
    {
        var stages = NeedStages();
        foreach (var meter in new[] { NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep, NeedMeter.Health })
        {
            stages[StageSlot(meter)] = StageNow(meter);
        }
    }

    /// <summary>Sets a meter as the test aid <c>--set-need</c> does: the value is clamped, and the stages are taken as they are, with no event.</summary>
    /// <param name="meter">Which meter.</param>
    /// <param name="value">The value.</param>
    public void SetNeed(NeedMeter meter, double value)
    {
        Needs.Set(meter, value);
        SettleNeedStages();
    }

    // A save's needs back in (save version 7), or full needs when it has none (an older save). The load raises no event.
    internal void RestoreNeeds(NeedsSave? save)
    {
        Needs.Restore(save ?? new PlayerNeeds(Data.Needs).Capture());
        SettleNeedStages();
    }

    private int[] NeedStages()
    {
        if (_needStages is null)
        {
            _needStages = new int[4];
            SettleNeedStages();
        }

        return _needStages;
    }

    private static int StageSlot(NeedMeter meter) => meter switch
    {
        NeedMeter.Hunger => 0,
        NeedMeter.Thirst => 1,
        NeedMeter.Sleep => 2,
        NeedMeter.Health => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(meter), meter, "Stamina has no stages."),
    };

    private int StageNow(NeedMeter meter) => meter switch
    {
        NeedMeter.Hunger => Needs.StageOf(Drive.Hunger),
        NeedMeter.Thirst => Needs.StageOf(Drive.Thirst),
        NeedMeter.Sleep => Needs.StageOf(Drive.Sleep),
        _ => Needs.HealthStage,
    };

    private string DetailOf(NeedMeter meter) => meter switch
    {
        NeedMeter.Hunger => Data.Needs.Of(Drive.Hunger).Detail,
        NeedMeter.Thirst => Data.Needs.Of(Drive.Thirst).Detail,
        NeedMeter.Sleep => Data.Needs.Of(Drive.Sleep).Detail,
        _ => Data.Needs.Health.Detail,
    };
}
