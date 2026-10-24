using System.Text.RegularExpressions;
using Vocaterra.Game;
using Vocaterra.Game.Language.Lessons;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Sound;

/// <summary>What a block is made of, as far as the gathering sounds go.</summary>
public enum SoundMaterial
{
    /// <summary>Trunks, planks, timber, crates, fences, signs, doors, the stall.</summary>
    Wood,

    /// <summary>Stone, cobble, mossy stone, the fire ring.</summary>
    Stone,

    /// <summary>Leaves, bushes, undergrowth, flowers, wheat.</summary>
    Leaf,

    /// <summary>Grass, dirt, sand, paths, snow, farmland, canvas.</summary>
    Soft,
}

/// <summary>
/// Turns what happens in the game into the cues that sound for it (S24). Engine free and clock free: the same events give the same cues. The mapping reads
/// events; it never writes to the simulation, a turn or the learner, so a sound cannot change a verdict, a state hash or the replay.
/// </summary>
/// <remarks>
/// <b>Never punishing.</b> An answer the NPC did not understand, an uncertain recognition, a "try again", a "no sé" and a request for help all map to the neutral cue
/// (<see cref="SoundCue.AnswerNeutral"/>), a soft tone with no fall and no buzz; a skipped lesson step maps to nothing.
/// </remarks>
public sealed partial class SoundEventMapper
{
    /// <summary>The gathering hit sounds every this many ticks (four hits a second) of holding (the first tick of a gather always sounds).</summary>
    public const int GatherHitInterval = 15;

    private readonly LessonBook? _lessons;

    /// <summary>Creates the mapper.</summary>
    /// <param name="lessons">The lessons, for telling a unit's last step from the others, or <see langword="null"/> (every step is then a plain step).</param>
    public SoundEventMapper(LessonBook? lessons = null) => _lessons = lessons;

    /// <summary>The material of a block, for its hit and break sounds.</summary>
    /// <param name="block">The block.</param>
    public static SoundMaterial MaterialOf(BlockId block) => block switch
    {
        BlockId.Trunk or BlockId.SpruceTrunk or BlockId.BirchTrunk or BlockId.Planks or BlockId.Timber or BlockId.Crate or BlockId.Fence or BlockId.Sign
            or BlockId.Bridge or BlockId.Door or BlockId.Table or BlockId.Stall or BlockId.Wall or BlockId.Roof or BlockId.Chest or BlockId.Torch or BlockId.Lantern => SoundMaterial.Wood,
        BlockId.Stone or BlockId.MossyStone or BlockId.Cobble or BlockId.FireRing => SoundMaterial.Stone,
        BlockId.Leaves or BlockId.SpruceLeaves or BlockId.BirchLeaves or BlockId.Bush or BlockId.BerryBush or BlockId.TallGrass or BlockId.Fern or BlockId.Reeds
            or BlockId.YellowFlower or BlockId.WhiteFlower or BlockId.Wheat or BlockId.Loaves => SoundMaterial.Leaf,
        _ => SoundMaterial.Soft,
    };

    /// <summary>The cue of a hit on a block of this material.</summary>
    /// <param name="material">The material.</param>
    public static SoundCue HitOf(SoundMaterial material) => material switch
    {
        SoundMaterial.Wood => SoundCue.GatherWood,
        SoundMaterial.Stone => SoundCue.GatherStone,
        SoundMaterial.Leaf => SoundCue.GatherLeaf,
        _ => SoundCue.GatherSoft,
    };

    /// <summary>The cue of a block of this material breaking.</summary>
    /// <param name="material">The material.</param>
    public static SoundCue BreakOf(SoundMaterial material) => material switch
    {
        SoundMaterial.Wood => SoundCue.BreakWood,
        SoundMaterial.Stone => SoundCue.BreakStone,
        SoundMaterial.Leaf => SoundCue.BreakLeaf,
        _ => SoundCue.BreakSoft,
    };

    /// <summary>The cues of one simulation event (the world's own events of a tick: gathering, placing, picking up, crafting, the bottle).</summary>
    /// <param name="e">The event.</param>
    /// <param name="blockAt">Reads the block at a cell (the hit sound depends on what is being gathered).</param>
    public IReadOnlyList<SoundCue> ForSimulation(SimulationEvent e, Func<int, int, int, BlockId> blockAt)
    {
        ArgumentNullException.ThrowIfNull(blockAt);
        switch (e.Kind)
        {
            case SimulationEventKind.GatherProgress when (e.Detail - 1) % GatherHitInterval == 0:
                return [HitOf(MaterialOf(blockAt(e.X, e.Y, e.Z)))];
            case SimulationEventKind.BlockRemoved:
                return [BreakOf(MaterialOf((BlockId)e.Detail))];
            case SimulationEventKind.BlockPlaced:
                return [SoundCue.PlaceBlock];
            case SimulationEventKind.ItemPickedUp:
                return [SoundCue.PickUp];
            case SimulationEventKind.ItemCrafted:
                return [SoundCue.CraftDone];
            case SimulationEventKind.BottleFilled:
                return [SoundCue.BottleFill];
            default:
                return [];
        }
    }

    /// <summary>
    /// The cues of one game event (the session's list: lesson steps, the objective, the fire, the ending, the voice events). The panels opening and closing, the
    /// buttons and the book are the front-end's own and are not events.
    /// </summary>
    /// <param name="e">The event.</param>
    public IReadOnlyList<SoundCue> ForGameEvent(GameEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        switch (e)
        {
            case LessonEvent { Kind: LessonEventKind.LessonCompleted }:
                return [SoundCue.LessonDone];
            case LessonEvent { Kind: LessonEventKind.StepDone } step:
                return StepCue(step);
            case VoiceEvent { Kind: VoiceEventKind.MicCheckPassed }:
                return [SoundCue.AnswerUnderstood];
            case VoiceEvent { Kind: VoiceEventKind.MicCheckFailed }:
                return [SoundCue.AnswerNeutral];
            case WorldEvent world:
                return world.Name switch
                {
                    WorldEvents.ObjectiveStep => [SoundCue.ObjectiveStep],
                    WorldEvents.ObjectiveComplete => [SoundCue.ObjectiveDone],
                    WorldEvents.SessionComplete => [SoundCue.SessionDone],
                    WorldEvents.FireLit => [SoundCue.FireLight],
                    WorldEvents.ItemGiven => [SoundCue.PickUp],
                    _ => [],
                };
            default:
                return [];
        }
    }

    /// <summary>
    /// The cues of a finished turn: understood is positive; not understood or uncertain, "no sé" and "ayuda" are the neutral cue, and a new known word is the sparkle.
    /// A check answer is the check cue whatever the answer was.
    /// </summary>
    /// <param name="turn">The turn, or <see langword="null"/> when the request failed.</param>
    /// <param name="words">The word progress of the turn, or <see langword="null"/>.</param>
    /// <param name="isCheck">Whether the turn answered a check item (S11).</param>
    public IReadOnlyList<SoundCue> ForTurn(GuideTurn? turn, WordTurnResult? words, bool isCheck = false)
    {
        if (isCheck)
        {
            return [SoundCue.CheckAnswered];
        }

        if (turn is null)
        {
            return [];
        }

        var cues = new List<SoundCue>();
        if (turn.Intent is "dont_know" or "ask_help" || !turn.Communicated || turn.Uncertain)
        {
            cues.Add(SoundCue.AnswerNeutral);
        }
        else
        {
            cues.Add(SoundCue.AnswerUnderstood);
        }

        if (words is { NewlyKnown.Count: > 0 })
        {
            cues.Add(SoundCue.WordLearnt);
        }

        return cues;
    }

    /// <summary>
    /// Folds the cues of one frame into what is played: a duplicate plays once, and the progress cues do not pile up (the answer's chime gives way to a step's, a step's to a
    /// unit's, up to the session's closing cue); the neutral cue gives way to anything positive in the same frame. The order of the rest is kept.
    /// </summary>
    /// <param name="cues">The cues of the frame, in the order they came.</param>
    public static IReadOnlyList<SoundCue> Collapse(IEnumerable<SoundCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var distinct = cues.Distinct().ToList();
        var top = distinct.Where(static c => ProgressRank(c) > 0).OrderByDescending(ProgressRank).FirstOrDefault();
        var result = new List<SoundCue>();
        foreach (var cue in distinct)
        {
            var rank = ProgressRank(cue);
            if (rank > 0 && cue != top)
            {
                continue;
            }

            if (cue == SoundCue.AnswerNeutral && distinct.Any(static c => c is SoundCue.AnswerUnderstood or SoundCue.WordLearnt))
            {
                continue;
            }

            result.Add(cue);
        }

        return result;
    }

    // The higher cue wins the frame. The sparkle is its own layer and is not ranked.
    private static int ProgressRank(SoundCue cue) => cue switch
    {
        SoundCue.SessionDone => 8,
        SoundCue.LessonDone => 7,
        SoundCue.ObjectiveDone => 6,
        SoundCue.UnitDone => 5,
        SoundCue.StepDone => 4,
        SoundCue.ObjectiveStep => 3,
        SoundCue.AnswerUnderstood => 2,
        _ => 0,
    };

    private IReadOnlyList<SoundCue> StepCue(LessonEvent step)
    {
        if (step.Outcome == StepOutcome.Skipped)
        {
            return []; // a skip is the learner's choice, not an achievement; nothing sounds
        }

        if (_lessons?.Find(step.Lesson) is not { } lesson || step.Step < 1 || step.Step > lesson.Steps.Count)
        {
            return [SoundCue.StepDone];
        }

        if (step.Step == lesson.Steps.Count)
        {
            return []; // the last step: the lesson's own cue follows
        }

        var here = UnitOf(lesson.Steps[step.Step - 1].Id);
        var next = UnitOf(lesson.Steps[step.Step].Id);
        return [here is not null && !string.Equals(here, next, StringComparison.Ordinal) ? SoundCue.UnitDone : SoundCue.StepDone];
    }

    // A step's unit is the number in its id: "u3-ask-2" is unit 3.
    private static string? UnitOf(string stepId) => UnitPrefix().Match(stepId) is { Success: true } match ? match.Groups[1].Value : null;

    [GeneratedRegex("^u(\\d+)-", RegexOptions.CultureInvariant)]
    private static partial Regex UnitPrefix();
}
