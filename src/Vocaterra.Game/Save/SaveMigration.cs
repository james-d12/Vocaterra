using System.Globalization;
using Vocaterra.Game.Language.Lessons;
using Vocaterra.Game.Npc;
using Vocaterra.Game.Session;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Save;

/// <summary>What the load of one area of a save did across bundles: how many records were kept and which were dropped (X01).</summary>
/// <param name="Area">The area (<c>phrases</c>, <c>lessons</c>, <c>words</c>, <c>world edits</c>...).</param>
/// <param name="Kept">How many records were kept.</param>
/// <param name="Dropped">The records that were dropped, one short description each (an id, with the reason when it is not the obvious one).</param>
public sealed record SaveMigrationArea(string Area, int Kept, IReadOnlyList<string> Dropped);

/// <summary>
/// The report of a load across bundles (X01): a save made from another content bundle was put into this one. It lists, area by area, what was kept and what was
/// dropped, and notes anything else worth a line in the session log (the world's digest, a player moved out of a wall, a lesson put back a step). Engine-free:
/// the front-end prints <see cref="Lines"/> and shows <see cref="Toast"/>; the tests read the areas.
/// </summary>
/// <param name="SavedBundle">The bundle hash the save was made from.</param>
/// <param name="CurrentBundle">The bundle hash of this run.</param>
/// <param name="WorldDigestMatched">Whether the restored world's digest equals the save's (true when the world generation did not change).</param>
/// <param name="Areas">One entry per area, in a fixed order.</param>
/// <param name="Notes">Other lines for the log.</param>
public sealed record SaveMigrationReport(
    string SavedBundle,
    string CurrentBundle,
    bool WorldDigestMatched,
    IReadOnlyList<SaveMigrationArea> Areas,
    IReadOnlyList<string> Notes)
{
    /// <summary>How many records were dropped in all.</summary>
    public int DroppedCount => Areas.Sum(static a => a.Dropped.Count);

    /// <summary>How many records were kept in all.</summary>
    public int KeptCount => Areas.Sum(static a => a.Kept);

    /// <summary>The area with this name, or <see langword="null"/>.</summary>
    /// <param name="area">The area's name.</param>
    public SaveMigrationArea? Find(string area) => Areas.FirstOrDefault(a => string.Equals(a.Area, area, StringComparison.Ordinal));

    /// <summary>The one line the player sees on screen (English).</summary>
    public string Toast => DroppedCount == 0
        ? "The game was updated. Your progress was kept."
        : string.Create(CultureInfo.InvariantCulture, $"The game was updated. Your progress was kept; {DroppedCount} old {(DroppedCount == 1 ? "record" : "records")} no longer in the game {(DroppedCount == 1 ? "was" : "were")} dropped.");

    /// <summary>The lines for the console and the session log: the summary, then each area, then every dropped record and note.</summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>
        {
            $"migrate: the save was made from bundle {Short(SavedBundle)}; this run has bundle {Short(CurrentBundle)}; the progress was kept where its ids still exist ({KeptCount} kept, {DroppedCount} dropped)",
        };
        foreach (var area in Areas)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"migrate: {area.Area}: kept {area.Kept}, dropped {area.Dropped.Count}"));
            lines.AddRange(area.Dropped.Select(d => $"migrate:   dropped {area.Area}: {d}"));
        }

        lines.AddRange(Notes.Select(static n => "migrate: " + n));
        return lines;
    }

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;
}

/// <summary>
/// Puts a save made from another content bundle into this one (X01). The content-hash rule of S10 refused such a save, so every content change made every save
/// unloadable. Nothing is refused here: the parts of the save that name content (words, phrases, lessons and their steps, tips, the objective's steps, the NPCs'
/// learner skills, the arrow) are kept where their ids exist in this bundle and dropped where they do not; new content starts empty, as in a fresh game. The
/// world's edits are applied by position (see <see cref="ApplyWorld"/>). Strict mode (the playtest's frozen bundle, D-game-10) never comes here.
/// </summary>
public static class SaveMigration
{
    /// <summary>The name of the area of world edits in a report.</summary>
    public const string WorldArea = "world edits";

    /// <summary>
    /// Applies the scene diff of a save over a freshly built scene of this bundle, by position. The save stored whole chunks of the world it was made from; the
    /// scene of this bundle may be built differently, so a stored cell is applied only when it is a change a player can make to the cell the new scene has:
    /// a gathered block (the cell now holds what a gather leaves, air or a plain bush), or a placed block (planks or a torch) in a cell of air or undergrowth that
    /// touches a block. A stored cell that differs from the new scene in any other way is the old world generation, not the player's work: it is skipped, and
    /// the new scene's block stays. When the save names the grid's chunk counts the chunk indices are decoded with them, so a world that grew or shrank still
    /// maps by position; a save without them (before X01) is decoded with this grid's counts.
    /// </summary>
    /// <param name="grid">The fresh grid of this bundle's scene (changed).</param>
    /// <param name="edits">The save's chunk edits.</param>
    /// <param name="saved">The chunk counts of the grid the save was taken from, or <see langword="null"/> when the save does not say.</param>
    /// <param name="report">Collects what was applied and skipped.</param>
    internal static void ApplyWorld(VoxelGrid grid, IReadOnlyList<ChunkEdit> edits, WorldDims? saved, Collector report)
    {
        var dims = saved ?? new WorldDims(grid.ChunksX, grid.ChunksY, grid.ChunksZ);
        var pending = new List<(int X, int Y, int Z, BlockId Block)>();
        var skipped = new List<string>();
        foreach (var edit in edits)
        {
            var count = dims.ChunksX * dims.ChunksY * dims.ChunksZ;
            if ((uint)edit.Chunk >= (uint)count)
            {
                skipped.Add($"chunk {edit.Chunk} (outside the world)");
                continue;
            }

            var cx = edit.Chunk % dims.ChunksX;
            var cz = edit.Chunk / dims.ChunksX % dims.ChunksZ;
            var cy = edit.Chunk / (dims.ChunksX * dims.ChunksZ);
            var position = 0;
            foreach (var run in edit.Runs)
            {
                for (var end = position + run.Length; position < end && position < VoxelChunk.Volume; position++)
                {
                    var x = (cx << VoxelChunk.Shift) + (position & (VoxelChunk.Size - 1));
                    var z = (cz << VoxelChunk.Shift) + ((position >> VoxelChunk.Shift) & (VoxelChunk.Size - 1));
                    var y = (cy << VoxelChunk.Shift) + (position >> (2 * VoxelChunk.Shift));
                    if (Enum.IsDefined(run.Block) && grid.InBounds(x, y, z) && grid.GetBlock(x, y, z) != run.Block)
                    {
                        pending.Add((x, y, z, run.Block));
                    }
                }
            }
        }

        var applied = 0;
        var placed = new List<(int X, int Y, int Z, BlockId Block)>();
        foreach (var cell in pending)
        {
            var now = grid.GetBlock(cell.X, cell.Y, cell.Z);
            if (IsGather(now, cell.Block))
            {
                VoxelGridDiff.SetCell(grid, cell.X, cell.Y, cell.Z, cell.Block);
                applied++;
            }
            else if (IsPlaceable(cell.Block) && IsEmptyOrDecor(now))
            {
                placed.Add(cell);
            }
            else
            {
                skipped.Add($"({cell.X}, {cell.Y}, {cell.Z}) {cell.Block}");
            }
        }

        // A placed block must touch another block; the ones that rest on blocks placed in the same save are tried again until none more can be set.
        bool progress;
        do
        {
            progress = false;
            for (var i = placed.Count - 1; i >= 0; i--)
            {
                var cell = placed[i];
                if (HasNeighbour(grid, cell.X, cell.Y, cell.Z))
                {
                    VoxelGridDiff.SetCell(grid, cell.X, cell.Y, cell.Z, cell.Block);
                    placed.RemoveAt(i);
                    applied++;
                    progress = true;
                }
            }
        }
        while (progress && placed.Count > 0);
        skipped.AddRange(placed.Select(static c => $"({c.X}, {c.Y}, {c.Z}) {c.Block} (nothing to rest on)"));

        report.Keep(WorldArea, applied);
        foreach (var skip in SummariseSkipped(skipped))
        {
            report.Drop(WorldArea, skip);
        }
    }

    // A gather in the old world: the cell now holds what the new scene's block becomes when gathered.
    private static bool IsGather(BlockId now, BlockId saved)
    {
        var definition = BlockDefinition.Get(now);
        return definition.Gatherable && definition.BecomesWhenGathered == saved;
    }

    private static bool IsPlaceable(BlockId block) => block is BlockId.Planks or BlockId.Torch;

    private static bool IsEmptyOrDecor(BlockId id) => id == BlockId.Air || BlockDefinition.IsDecor(id);

    private static bool HasNeighbour(VoxelGrid grid, int x, int y, int z)
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            var next = new BlockPos(x, y, z).Offset(face);
            if (grid.InBounds(next.X, next.Y, next.Z) && !IsEmptyOrDecor(grid.GetBlock(next.X, next.Y, next.Z)))
            {
                return true;
            }
        }

        return false;
    }

    // A changed world can skip thousands of cells; the log gets the first few and a count.
    private static IEnumerable<string> SummariseSkipped(List<string> skipped)
    {
        const int Shown = 5;
        foreach (var skip in skipped.Take(Shown))
        {
            yield return skip;
        }

        if (skipped.Count > Shown)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"and {skipped.Count - Shown} more cells that are not the player's edits (the new world generation differs there)");
        }
    }

    /// <summary>
    /// Filters the parts of a save that name content down to what this session's bundle has, and reports the rest. The session is already made on the
    /// scene with the world edits applied. The returned document is the one <see cref="SaveGame"/> then restores part by part.
    /// </summary>
    /// <param name="document">The save.</param>
    /// <param name="session">The new session (fresh; nothing restored yet).</param>
    /// <param name="report">Collects the counts.</param>
    internal static SaveDocument Adapt(SaveDocument document, VoxelSession session, Collector report)
    {
        var words = Filter("words", document.Words, static w => w.Lemma, session.Nouns.ForLemma, report);
        var carded = Filter("carded nouns", document.Carded, static l => l, session.Nouns.ForLemma, report);
        var phrases = Filter("phrases", document.Phrases, static p => p.Id, session.Phrases.Find, report);
        var objective = Filter("objective steps", document.Objective, static o => o.Key, session.Objective.Step, report);
        var npcs = AdaptNpcs(document.Npcs, session, report);
        var deferred = document.DeferredSkills.Where(s => KnownSkill(s.Skill, session)).ToArray();
        ReportCount("deferred skills", document.DeferredSkills.Count, deferred.Length, document.DeferredSkills.Where(s => !KnownSkill(s.Skill, session)).Select(static s => s.Skill), report);
        var pointer = document.Pointer is { } p && session.Nouns.ForLemma(p.Place) is null ? null : document.Pointer;
        if (document.Pointer is not null && pointer is null)
        {
            report.Drop("arrow", document.Pointer.Place);
        }

        var inventory = AdaptInventory(document.Inventory, report);
        var lessons = document.Lessons is { } l ? AdaptLessons(l, session, report) : null;
        var tips = document.Tips is { } t ? AdaptTips(t, session, report) : null;
        var gather = document.Gather;
        if (gather.Target is { } target && (!session.Scene.Grid.InBounds(target.X, target.Y, target.Z) || session.Scene.Grid.GetBlock(target.X, target.Y, target.Z) != gather.Block))
        {
            gather = new GatherSave(null, BlockId.Air, 0);
            report.Note("the block being gathered is not there in the new world: the gathering stopped");
        }

        return document with
        {
            Words = words,
            Carded = carded,
            Phrases = phrases,
            Objective = objective,
            Npcs = npcs,
            DeferredSkills = deferred,
            Pointer = pointer,
            Inventory = inventory,
            Lessons = lessons,
            Tips = tips,
            Gather = gather,
        };
    }

    // The learner's own record (history, word rows, settings) names no bundle content, so it is kept whole.
    private static T[] Filter<T>(string area, IEnumerable<T> items, Func<T, string> id, Func<string, object?> find, Collector report)
    {
        var all = items.ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<T>();
        var dropped = new List<string>();
        foreach (var item in all)
        {
            if (find(id(item)) is not null && seen.Add(id(item)))
            {
                kept.Add(item);
            }
            else
            {
                dropped.Add(id(item));
            }
        }

        ReportCount(area, all.Length, kept.Count, dropped, report);
        return [.. kept];
    }

    private static void ReportCount(string area, int total, int kept, IEnumerable<string> dropped, Collector report)
    {
        _ = total;
        report.Keep(area, kept);
        foreach (var id in dropped)
        {
            report.Drop(area, id);
        }
    }

    private static bool KnownSkill(string skill, VoxelSession session)
    {
        if (skill.StartsWith(NpcService.PhraseSkillPrefix, StringComparison.Ordinal))
        {
            return session.Phrases.Find(skill[NpcService.PhraseSkillPrefix.Length..]) is not null;
        }

        return !skill.StartsWith("word.", StringComparison.Ordinal) || session.Nouns.ForLemma(skill["word.".Length..]) is not null;
    }

    // Each NPC of the game at most once; the learner model of each keeps the skills this bundle still has.
    private static NpcSave[] AdaptNpcs(IReadOnlyList<NpcSave> npcs, VoxelSession session, Collector report)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<NpcSave>();
        var droppedNpcs = new List<string>();
        var droppedSkills = new List<string>();
        var keptSkills = 0;
        foreach (var npc in npcs)
        {
            if (!NpcService.AgentIds.Contains(npc.Agent, StringComparer.Ordinal) || !seen.Add(npc.Agent))
            {
                droppedNpcs.Add(npc.Agent);
                continue;
            }

            var skills = npc.Learner.Skills.Where(s => KnownSkill(s.SkillId, session)).ToArray();
            keptSkills += skills.Length;
            droppedSkills.AddRange(npc.Learner.Skills.Where(s => !KnownSkill(s.SkillId, session)).Select(s => $"{npc.Agent}: {s.SkillId}"));
            result.Add(npc with { Learner = npc.Learner with { Skills = skills } });
        }

        ReportCount("npcs", result.Count, result.Count, droppedNpcs, report);
        ReportCount("learner-model skills", keptSkills + droppedSkills.Count, keptSkills, droppedSkills, report);
        return [.. result];
    }

    // Items are an enum of the game's code, not of the bundle: a slot with an item this build does not know becomes an empty slot.
    private static SlotSave[] AdaptInventory(IReadOnlyList<SlotSave> slots, Collector report)
    {
        var dropped = new List<string>();
        var kept = 0;
        var result = slots.Select(slot =>
        {
            if (slot.Count > 0 && !Enum.IsDefined(slot.Item))
            {
                dropped.Add(slot.Item.ToString());
                return new SlotSave(default, 0);
            }

            kept += slot.Count > 0 ? 1 : 0;
            return slot;
        }).ToArray();
        ReportCount("inventory slots", kept + dropped.Count, kept, dropped, report);
        return result;
    }

    private static TipsSave AdaptTips(TipsSave tips, VoxelSession session, Collector report)
    {
        var shown = tips.Shown.Where(session.Tips.Knows).ToArray();
        var queuedAt = Enumerable.Range(0, tips.Queued.Count).Where(i => session.Tips.Knows(tips.Queued[i])).ToArray();
        var current = tips.Current is { } c && session.Tips.Knows(c) ? c : null;
        var all = tips.Shown.Concat(tips.Queued).Concat(tips.Current is null ? [] : [tips.Current]).Distinct(StringComparer.Ordinal).ToArray();
        var keptIds = shown.Concat(queuedAt.Select(i => tips.Queued[i])).Concat(current is null ? [] : [current]).Distinct(StringComparer.Ordinal).ToArray();
        ReportCount("tips", all.Length, keptIds.Length, all.Except(keptIds, StringComparer.Ordinal), report);
        return new TipsSave(
            shown,
            [.. queuedAt.Select(i => tips.Queued[i])],
            current,
            current is null ? 0 : tips.Since,
            tips.Ready is { } ready && ready.Count == tips.Queued.Count ? [.. queuedAt.Select(i => ready[i])] : null);
    }

    /// <summary>
    /// Maps the lessons' progress by step id. A lesson that is gone is dropped (and so is the active mark or the replay that named it). For a lesson that
    /// stays: a completed one stays completed with the outcomes of the steps that still exist; one in progress keeps the longest run of its steps, from the
    /// first, whose ids are all among the finished ones, and goes on at the step after it (the tries on the step in progress are reset when the step is
    /// not the one it was). A finished step that follows a step the lesson does not have finished is done again.
    /// </summary>
    private static LessonsSave AdaptLessons(LessonsSave save, VoxelSession session, Collector report)
    {
        var book = session.Lessons.Book;
        var result = new List<LessonProgressSave>();
        var droppedLessons = new List<string>();
        var droppedSteps = new List<string>();
        var keptSteps = 0;
        foreach (var progress in save.Lessons)
        {
            if (book.Find(progress.Id) is not { } lesson || result.Any(r => string.Equals(r.Id, progress.Id, StringComparison.Ordinal)))
            {
                droppedLessons.Add(progress.Id);
                continue;
            }

            var stepIds = lesson.Steps.Select(static s => s.Id).ToArray();
            var outcomes = progress.Outcomes.Where(o => stepIds.Contains(o.StepId, StringComparer.Ordinal)).DistinctBy(static o => o.StepId, StringComparer.Ordinal).ToArray();
            droppedSteps.AddRange(progress.Outcomes.Where(o => !outcomes.Contains(o)).Select(o => $"{progress.Id}/{o.StepId}"));
            if (progress.Status == LessonStatus.InProgress)
            {
                var prefix = 0;
                while (prefix < stepIds.Length && outcomes.Any(o => string.Equals(o.StepId, stepIds[prefix], StringComparison.Ordinal)))
                {
                    prefix++;
                }

                if (prefix >= stepIds.Length)
                {
                    prefix = stepIds.Length - 1; // every step is finished in the new lesson but it was not complete: the last step is done again
                }

                var ordered = stepIds.Take(prefix).Select(id => outcomes.First(o => string.Equals(o.StepId, id, StringComparison.Ordinal))).ToArray();
                droppedSteps.AddRange(outcomes.Where(o => !ordered.Contains(o)).Select(o => $"{progress.Id}/{o.StepId} (done again)"));
                keptSteps += ordered.Length;
                var sameStep = prefix == progress.Step && ordered.Length == progress.Outcomes.Count;
                result.Add(progress with
                {
                    Step = prefix,
                    Outcomes = ordered,
                    Attempts = sameStep ? progress.Attempts : 0,
                    Helped = sameStep && progress.Helped,
                });
            }
            else
            {
                // Not started or completed: the outcomes of the steps that remain, in the lesson's order; a completed lesson rests on its last step.
                var ordered = stepIds.Where(id => outcomes.Any(o => string.Equals(o.StepId, id, StringComparison.Ordinal)))
                    .Select(id => outcomes.First(o => string.Equals(o.StepId, id, StringComparison.Ordinal))).ToArray();
                keptSteps += ordered.Length;
                result.Add(progress with
                {
                    Step = progress.Status == LessonStatus.Completed ? stepIds.Length - 1 : 0,
                    Outcomes = ordered,
                    Attempts = 0,
                    Helped = false,
                });
            }
        }

        ReportCount("lessons", result.Count, result.Count, droppedLessons, report);
        ReportCount("lesson steps", keptSteps + droppedSteps.Count, keptSteps, droppedSteps, report);
        var active = save.Active is { } id && result.Any(r => string.Equals(r.Id, id, StringComparison.Ordinal) && r.Status == LessonStatus.InProgress) ? id : null;
        if (save.Active is not null && active is null)
        {
            report.Note($"the active lesson '{save.Active}' is no longer in the game: no lesson is active");
        }

        var replay = save.Replay is { } r && active is null && result.Any(x => string.Equals(x.Id, r, StringComparison.Ordinal) && (x.EverCompleted ?? x.Status == LessonStatus.Completed)) ? r : null;
        if (save.Replay is not null && replay is null)
        {
            report.Note($"the lesson '{save.Replay}' to practise again is no longer a done lesson of the game: the replay was cancelled");
        }

        return new LessonsSave(result, active, replay);
    }

    /// <summary>The counts and drops collected while a save is migrated.</summary>
    internal sealed class Collector
    {
        private readonly List<string> _order = [];
        private readonly Dictionary<string, int> _kept = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _dropped = new(StringComparer.Ordinal);
        private readonly List<string> _notes = [];

        public void Keep(string area, int count)
        {
            Touch(area);
            _kept[area] += count;
        }

        public void Drop(string area, string id)
        {
            Touch(area);
            _dropped[area].Add(id);
        }

        public void Note(string note) => _notes.Add(note);

        public SaveMigrationReport Build(string savedBundle, string currentBundle, bool digestMatched) =>
            new(savedBundle, currentBundle, digestMatched, _order.Select(a => new SaveMigrationArea(a, _kept[a], _dropped[a])).ToArray(), _notes.ToArray());

        private void Touch(string area)
        {
            if (!_kept.ContainsKey(area))
            {
                _order.Add(area);
                _kept[area] = 0;
                _dropped[area] = [];
            }
        }
    }
}
