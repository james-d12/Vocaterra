using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.World;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Save;

/// <summary>A save that cannot be read, is for another world or bundle, or does not restore to the digest it was taken with.</summary>
public sealed class SaveException : Exception
{
    /// <summary>Creates the exception with a message for the player (the front-end prints it).</summary>
    /// <param name="message">What is wrong with the save.</param>
    public SaveException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Saves and loads a running game (S10; version 2 with the learner's record, S19; version 3 without the check answers in the word record, S19b; versions 1 and 2 still load). <see cref="Capture"/> reads a session at rest into a <see cref="SaveDocument"/> and
/// <see cref="Serialize"/> writes it as canonical JSON (sorted keys, no whitespace, invariant numbers): the same state gives the same bytes.
/// <see cref="Load"/> reads one back into a new session: it refuses a save of another seed or bundle, rebuilds the scene from the seed and the
/// layout, applies the scene diff, restores every part, checks the world's digest, and runs the session in the next play session (which lifts
/// the spacing gaps of D-game-6). The stored time continues where the save left it: loading adds nothing to it.
/// </summary>
/// <remarks>
/// No file is read or written here: the front-end reads the text and writes the bytes. Nothing reads the clock or draws a random number.
/// </remarks>
public static class SaveGame
{
    /// <summary>The format name every save carries.</summary>
    public const string FormatName = "vocaterra-save";

    /// <summary>
    /// The format version this code writes (version 2 adds the learner's record, S19; version 3 keeps the check answers out of its word rows, S19b; version 5 keys
    /// the objective's steps <c>&lt;objective&gt;.&lt;step&gt;</c>, C07; version 7 adds the optional <c>systems</c> map with the player's needs (N01), the NPCs met (L01), and the water that is not
    /// source in the scene diff, the cells waiting to flow and the player's air (water-plan H06). The two branches each wrote a different version 6 before they met; a save of either of those is not read).
    /// </summary>
    public const int CurrentVersion = 7;

    /// <summary>The objective the steps of a save before version 5 belong to: those saves held the camp checklist alone, keyed by the step (<c>wood</c>).</summary>
    public const string UnqualifiedObjective = "camp";

    /// <summary>The oldest format version this code still reads (version 1, before S19: no learner's record, so an empty history).</summary>
    public const int OldestReadableVersion = 1;

    /// <summary>The canonical JSON of a save (the same document always gives the same string).</summary>
    /// <param name="document">The document.</param>
    public static string Serialize(SaveDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CanonicalJson.Serialize(document);
    }

    /// <summary>Reads a save's text. Refuses text that is not a save of this format and version.</summary>
    /// <exception cref="SaveException">The text is not a readable save of version <see cref="CurrentVersion"/>.</exception>
    public static SaveDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        SaveDocument document;
        try
        {
            document = CanonicalJson.Deserialize<SaveDocument>(json);
        }
        catch (JsonException ex)
        {
            throw new SaveException($"The file is not a readable save: {ex.Message}");
        }

        if (!string.Equals(document.Format, FormatName, StringComparison.Ordinal))
        {
            throw new SaveException($"The file is not a {FormatName} save (its format is '{document.Format}').");
        }

        if (document.Version < OldestReadableVersion || document.Version > CurrentVersion)
        {
            throw new SaveException($"The save is format version {document.Version}; this game reads versions {OldestReadableVersion} to {CurrentVersion}.");
        }

        return document;
    }

    /// <summary>
    /// Checks that a save can be loaded here: the bundle is the one it was made from, and (when given) the world seed is the one asked for.
    /// </summary>
    /// <param name="document">The save.</param>
    /// <param name="bundle">The content bundle of this run.</param>
    /// <param name="requestedSeed">The seed the run asked for, or <see langword="null"/> to take the save's.</param>
    /// <param name="migrate">Whether a save of another bundle is let through to be migrated (X01, the game's default). <see langword="false"/> is strict mode: the refusal of S10, which the playtest's recall sessions keep.</param>
    /// <exception cref="SaveException">The seed differs, or the bundle differs and <paramref name="migrate"/> is off.</exception>
    public static void Check(SaveDocument document, ContentBundle bundle, ulong? requestedSeed = null, bool migrate = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bundle);
        if (requestedSeed is { } seed && seed != document.WorldSeed)
        {
            throw new SaveException($"The save is for world seed {document.WorldSeed}, not {seed}. Load it without --seed, or with --seed {document.WorldSeed}.");
        }

        if (!migrate && !string.Equals(document.BundleHash, bundle.ContentHash, StringComparison.Ordinal))
        {
            throw new SaveException(
                $"The save was made from content bundle {Short(document.BundleHash)}; this run has bundle {Short(bundle.ContentHash)}. "
                + "The game's content changed since the save, and strict save mode does not carry progress across content versions, so it cannot be loaded. "
                + "Load it with the bundle it was made from, or leave out --strict-save (and --playtest) to keep the progress in the updated game.");
        }
    }

    /// <summary>
    /// Captures a session as a save. The session must be at rest (<see cref="GuideSession.CanSave"/>: no turn on the worker, no key words standing,
    /// no opening waiting). <paramref name="pristine"/> is a freshly built scene of the same seed and layout, which the scene diff is taken against.
    /// The session is not changed.
    /// </summary>
    /// <param name="session">The running game.</param>
    /// <param name="pristine">The scene as it was built for the session's seed (a new build, never the session's own scene).</param>
    /// <param name="settings">The player's settings to keep in the save (S24: the sound volumes), or <see langword="null"/> for none.</param>
    /// <param name="dialoguePages">The NPCs' conversation pages (U08, <see cref="DialoguePageSave.Capture"/>), or <see langword="null"/> for none.</param>
    /// <exception cref="InvalidOperationException">The session is not at rest.</exception>
    /// <exception cref="ArgumentException">The pristine scene is not this session's scene.</exception>
    public static SaveDocument Capture(VoxelSession session, Scene pristine, SaveSettings? settings = null, IReadOnlyList<DialoguePageSave>? dialoguePages = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pristine);
        if (!session.CanSave)
        {
            throw new InvalidOperationException("The game is not at rest: the guide is still thinking, the key words are on show or an opening waits. Save in a moment.");
        }

        if (pristine.Seed != session.WorldSeed || !string.Equals(pristine.Name, session.Scene.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("The pristine scene is not built from this session's seed and layout.", nameof(pristine));
        }

        var state = session.Simulation.CaptureState();
        return new SaveDocument(
            FormatName,
            CurrentVersion,
            session.WorldSeed,
            session.Scene.Name,
            session.Bundle.ContentHash,
            session.StartedAt,
            session.SkippedSeconds,
            session.StoredTime.UnixSeconds,
            session.Day,
            state.Tick,
            session.Simulation.ComputeStateDigest(),
            state.SelectedSlot,
            new GatherSave(state.GatherTarget, state.GatherBlock, state.GatherProgress),
            state.NoBottleHint,
            state.Player,
            state.Slots.Select(static slot => new SlotSave(slot.Item, slot.Count)).ToArray(),
            session.ObjectiveCounts().Select(static step => new ObjectiveSave(step.Key, step.Count)).ToArray(),
            session.Words.Records.Values.OrderBy(static record => record.Lemma, StringComparer.Ordinal).Select(WordSaveOf).ToArray(),
            session.PhraseProgress.Records.Select(PhraseSaveOf).ToArray(),
            session.CardedLemmas(),
            session.KeyWordsTaughtBy.Contains(NpcService.ShopkeeperAgent), // V30: the old flag is the shopkeeper's, as older builds and this reader take it
            session.RiverPointedState,
            session.Pointer is { Quiz: false } pointer ? new PointerSave(pointer.Place, pointer.Target) : null,
            session.DeferredSkills.Select(static step => new DeferredSkillSave(step.Npc, step.Skill)).ToArray(),
            session.LastRepairs().Select(static step => new LastRepairSave(step.Npc, step.Repair)).ToArray(),
            session.NextTurnId,
            session.Data.Npcs.Ids.Select(session.Conversation.CaptureAgent).ToArray(),
            VoxelGridDiff.Capture(session.Scene.Grid, pristine.Grid),
            session.Lessons.Capture(),
            session.Tips.Capture(),
            session.Learner.Capture(),
            BookGranted: true,
            Settings: settings is null ? null : settings.Clamped(),
            World: new WorldDims(session.Scene.Grid.ChunksX, session.Scene.Grid.ChunksY, session.Scene.Grid.ChunksZ),
            SkippedTo: session.SkippedTo,
            Sitting: session.Sitting,
            KeyWordsTaughtBy: session.KeyWordsTaughtBy,
            Systems: new SystemsSave(session.Needs.Capture(), session.CaptureChest(), session.CaptureBushes(), session.CaptureGifts(), session.CapturePlanted(), session.CaptureProcedures()),
            MetNpcs: session.MetNpcs,
            Air: state.Air == Vocaterra.Game.Core.Water.Breath.MaxAir ? null : state.Air,
            Flow: state.Flow is { Count: > 0 } ? state.Flow : null,
            DialoguePages: dialoguePages is { Count: > 0 } ? dialoguePages : null);
    }

    /// <summary>Captures a session and serializes it: the text of the save file.</summary>
    /// <param name="session">The running game (at rest).</param>
    /// <param name="pristine">A freshly built scene of the same seed and layout.</param>
    /// <param name="settings">The player's settings to keep in the save (S24), or <see langword="null"/>.</param>
    public static string Save(VoxelSession session, Scene pristine, SaveSettings? settings = null) => Serialize(Capture(session, pristine, settings));

    /// <summary>
    /// Reads a save's text, checks it against the bundle and the requested seed, builds its scene with <paramref name="buildPristine"/> and restores
    /// the session (see <see cref="Restore"/>).
    /// </summary>
    /// <param name="json">The save's text.</param>
    /// <param name="data">The game's content of this run (its bundle is checked against the save).</param>
    /// <param name="buildPristine">Builds the scene of a seed from the layout (a new scene each call).</param>
    /// <param name="requestedSeed">The seed the run asked for, or <see langword="null"/> to take the save's.</param>
    /// <param name="credit">The credit rules of the run (the default when <see langword="null"/>).</param>
    /// <param name="bumpSession">Whether the run is the next play session (true for a real load; the tests also restore without it).</param>
    /// <param name="migrate">Whether a save of another bundle is migrated into this one (X01) rather than refused (strict mode, the default here).</param>
    /// <exception cref="SaveException">The save cannot be loaded.</exception>
    public static VoxelSession Load(
        string json,
        GameData data,
        Func<ulong, Scene> buildPristine,
        ulong? requestedSeed = null,
        CreditRules? credit = null,
        bool bumpSession = true,
        bool migrate = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(buildPristine);
        var document = Parse(json);
        Check(document, data.Bundle, requestedSeed, migrate);
        return Restore(document, data, buildPristine(document.WorldSeed), credit, bumpSession, migrate);
    }

    /// <summary>
    /// Restores a session from a save and a freshly built scene of the save's seed and layout. The scene diff is applied to the scene, the session is
    /// made on it, and every part is put back. The restored world's digest must equal the save's, and the stored time must be the save's, or the
    /// save is refused. The play session is the save's plus one when <paramref name="bumpSession"/> (a load), the save's own when not.
    /// </summary>
    /// <param name="document">The save.</param>
    /// <param name="data">The game's content of this run (its bundle is checked against the save).</param>
    /// <param name="scene">A freshly built scene of the save's seed and layout; it is changed by the diff and then becomes the session's scene.</param>
    /// <param name="credit">The credit rules of the run; the default when <see langword="null"/>.</param>
    /// <param name="bumpSession">Whether to start the next play session.</param>
    /// <param name="migrate">Whether a save of another bundle is migrated (X01): the parts that name content are kept where their ids exist, the world edits are applied by position, and
    /// the world's digest is reported rather than required (<see cref="VoxelSession.Migration"/> holds the report). A save of this very bundle is restored exactly as before, with or without it.</param>
    /// <exception cref="SaveException">The save does not fit the scene, the bundle or itself, or the restored world differs from its digest.</exception>
    public static VoxelSession Restore(
        SaveDocument document,
        GameData data,
        Scene scene,
        CreditRules? credit = null,
        bool bumpSession = true,
        bool migrate = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(scene);
        var bundle = data.Bundle;
        Check(document, bundle, null, migrate);
        if (document.Version < 5)
        {
            // Before version 5 the save held the camp checklist alone, keyed by the step; the keys are the objective's now (C07). Mapped here, before the migration,
            // which would drop a key it does not know. An old key has no dot, so a key that has one is left as it is.
            document = document with
            {
                Objective = document.Objective.Select(static step => step.Key.Contains('.', StringComparison.Ordinal) ? step : step with { Key = UnqualifiedObjective + "." + step.Key }).ToArray(),
            };
        }

        var migrating = migrate && !string.Equals(document.BundleHash, bundle.ContentHash, StringComparison.Ordinal);
        var collector = migrating ? new SaveMigration.Collector() : null;
        if (document.Session < 1)
        {
            throw new SaveException($"The save's play session is {document.Session}; sessions start at 1.");
        }

        if (scene.Seed != document.WorldSeed || !string.Equals(scene.Name, document.SceneName, StringComparison.Ordinal))
        {
            throw new SaveException("The scene was not built from this save's seed and layout.");
        }

        VoxelSession session;
        try
        {
            if (collector is null)
            {
                VoxelGridDiff.Apply(scene.Grid, document.Chunks);
            }
            else
            {
                SaveMigration.ApplyWorld(scene.Grid, document.Chunks, document.World, collector);
            }

            session = new VoxelSession(data, document.WorldSeed, document.StartedAt, scene, credit);
            var spawn = session.Simulation.CaptureState();
            if (collector is not null)
            {
                document = SaveMigration.Adapt(document, session, collector);
            }

            RestoreParts(session, document, migrating);
            if (collector is not null)
            {
                KeepPlayerOutOfWalls(session, spawn, collector);
            }
        }
        catch (ArgumentException ex)
        {
            throw new SaveException($"The save does not restore: {ex.Message}");
        }

        if (session.StoredTime.UnixSeconds != document.StoredTime)
        {
            throw new SaveException($"The stored time is {session.StoredTime.UnixSeconds}, the save says {document.StoredTime}.");
        }

        var digest = session.Simulation.ComputeStateDigest();
        var digestMatched = string.Equals(digest, document.StateDigest, StringComparison.Ordinal);
        if (!digestMatched && collector is null)
        {
            throw new SaveException("The restored world does not match the save's digest: the save is damaged or was made by another build.");
        }

        if (collector is not null)
        {
            // The world's digest is the check of a save restored into the world it was made from. Across bundles the world generation may have changed, so the edits were
            // applied by position and the digest is only reported (it still matches when the generation is the same and the player was not moved).
            collector.Note(digestMatched
                ? "the world's digest matches the save's: the world generation is unchanged and every edit was applied as saved"
                : "the world's digest differs from the save's: the world generation changed (or the player was moved), so the edits were applied by position");
            session.Migration = collector.Build(document.BundleHash, bundle.ContentHash, digestMatched);
        }

        // A save from before S20 has no book in its slots, because the learner was only given it when a new game started (S22): the load gives it once. Done after the
        // digest check, which is the check of the save's own world; the next save says the book was granted. A bag with no free slot cannot take it, and the next load tries again.
        if (!document.BookGranted && session.Simulation.Inventory.Count(ItemId.Book) == 0)
        {
            _ = session.Simulation.GrantItems(ItemId.Book, 1);
        }

        // A load keeps the day as saved (V25): the objective, the orders and the lessons stand where the save left them, and only a sleep moves the day. The sitting is the
        // next one (S10), which lifts the spacing gaps; a save from before V25 has none, and its session is the sitting it continues from.
        session.Day = document.Session;
        session.Sitting = (document.Sitting ?? document.Session) + (bumpSession ? 1 : 0);
        session.SettleRest(); // the day goes on: what its objective said is not news (V12c), and the beacon is the save's

        return session;
    }

    /// <summary>
    /// The sleep at the player's own fire (V12c): the running session goes on as the next day (and the next sitting, so the spacing rule sees a different play session), in the state a load of its own save would give, without a new
    /// session or scene. <paramref name="document"/> is that save, captured now (<see cref="Capture"/>). What a save does not keep goes back to how a new session has it
    /// (the review question, the second tries, the warm-up, the word cards not shown, the buttons' English counts), every part of the save is put back as a load puts it
    /// back (the world is the save's already), and the session becomes the save's plus one, which lifts the spacing gaps and names the new session's lesson
    /// (<see cref="VoxelSession.BeginPlaySession"/>). The stored time does not move, as a load adds none.
    /// </summary>
    /// <param name="session">The running game, at rest, with no talk open.</param>
    /// <param name="document">The session's own save, captured now.</param>
    /// <param name="collapse">The night is a collapse (N03): the needs take the collapse's values, not the night's, and the player stays where they are (the world is the save's, taken where they stand).</param>
    /// <exception cref="InvalidOperationException">The session is not at rest.</exception>
    /// <exception cref="ArgumentException">The save is not this session's, taken now.</exception>
    public static void Sleep(VoxelSession session, SaveDocument document, bool collapse = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(document);
        if (!session.CanSave)
        {
            throw new InvalidOperationException("The game is not at rest: the guide is still thinking, the key words are on show or an opening waits. Sleep in a moment.");
        }

        if (document.WorldSeed != session.WorldSeed || !string.Equals(document.BundleHash, session.Bundle.ContentHash, StringComparison.Ordinal) || document.Session != session.Day || (document.Sitting ?? document.Session) != session.Sitting
            || document.Tick != session.Simulation.Tick || !string.Equals(document.StateDigest, session.Simulation.ComputeStateDigest(), StringComparison.Ordinal))
        {
            throw new ArgumentException("The save is not this session's own, taken now.", nameof(document));
        }

        session.ClearUnsaved();
        RestoreParts(session, document, migrating: false); // the needs come back as a load's do; the night's changes on them follow (ApplyNight)
        session.Day = document.Session + 1;
        session.Sitting = (document.Sitting ?? document.Session) + 1;
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.Slept, null, string.Create(CultureInfo.InvariantCulture, $"session {document.Session} to {session.Day}"))); // the new session's first event
        session.ApplyNight(collapse);
        session.BeginPlaySession();
    }

    // Every part of the save, put back in the order the parts depend on: the simulation, the time, the progress, the NPCs.
    private static void RestoreParts(VoxelSession session, SaveDocument document, bool migrating)
    {
        var slots = document.Inventory.Select(static slot => new ItemStack(slot.Item, slot.Count)).ToArray();
        session.Simulation.Restore(new VoxelSimulationState(
            document.Tick,
            document.SelectedSlot,
            document.Gather.Target,
            document.Gather.Block,
            document.Gather.Progress,
            document.NoBottleHint,
            document.Player,
            slots,
            document.Air ?? Vocaterra.Game.Core.Water.Breath.MaxAir,
            document.Flow));
        session.RestoreSkippedSeconds(document.SkippedSeconds);
        session.Words.Restore(document.Words.Select(RecordOf));
        RestorePhrases(session, document.Phrases, migrating);
        session.RestoreObjectiveCounts(document.Objective.Select(static step => (step.Key, step.Count)));
        session.RestoreCarded(document.Carded);
        session.RestoreMet(document.MetNpcs ?? (document.Session >= 2 ? session.Data.Npcs.Ids : [NpcService.GuideAgent])); // L01: a save from before has none
        session.RestoreKeyWordsTaught(document.KeyWordsTaughtBy ?? (document.KeyWordsTaught ? [NpcService.ShopkeeperAgent] : [])); // an older save's flag was the shopkeeper's (V23a)
        session.RiverPointedState = document.RiverPointed;
        session.RestorePointer(document.Pointer is { } pointer ? new PointerMarker(pointer.Place, pointer.Target, Point: session.PointNameAt(pointer.Target, pointer.Place)) : null); // V02: the place's name is worked out again
        session.RestoreDeferredSkills(document.DeferredSkills.Select(static step => (step.Npc, step.Skill)));
        session.RestoreLastRepairs(document.LastRepairs.Select(static step => (step.Npc, step.Repair)));
        session.NextTurnId = document.NextTurnId;
        RestoreNpcs(session, document.Npcs, migrating);
        if (document.Lessons is { } lessons)
        {
            session.Lessons.Restore(lessons);
        }

        if (document.Tips is { } tips)
        {
            session.Tips.Restore(tips);
        }

        if (document.Learner is { } learner)
        {
            session.Learner.Restore(learner, checksCounted: document.Version < 3); // a version 1 save has none: the learner's history starts empty (S19); a version 2 save's rows counted the checks (S19b)
        }

        session.RestoreChest(document.Systems?.Chest); // N04: the chest's block is in the world's edits; here its cell and contents
        session.RestoreRenewal(document.Systems?.Bushes, document.Systems?.Gifts); // N06: the picked bushes' blocks are in the world's edits; here the cells that grow back, and the gifts given today
        session.RestorePlanted(document.Systems?.Planted); // N08: the planted bushes that stand, for the camp's cap
        session.RestoreProcedures(document.Systems?.Procedures); // W06: a save without an entry has none
        session.RestoreNeeds(document.Systems?.Needs); // N01: a save before version 7 has none, so full needs; the needs are not in the world's digest
        session.SkippedTo = document.SkippedTo; // a test run skipped to a later session stays marked (V12c)
        session.RestoreStage(); // the stage of the objective area (S17) follows from the world and the progress just put back
    }

    // A world that was generated differently may have a wall where the player stood: the player goes back to the new scene's start.
    private static void KeepPlayerOutOfWalls(VoxelSession session, VoxelSimulationState spawn, SaveMigration.Collector collector)
    {
        const int U = VoxelPhysics.UnitsPerBlock;
        var body = session.Simulation.Player;
        var grid = session.Scene.Grid;
        for (var x = FloorDiv(body.MinX, U); x <= FloorDiv(body.MaxX - 1, U); x++)
        {
            for (var y = FloorDiv(body.MinY, U); y <= FloorDiv(body.MaxY - 1, U); y++)
            {
                for (var z = FloorDiv(body.MinZ, U); z <= FloorDiv(body.MaxZ - 1, U); z++)
                {
                    if (grid.InBounds(x, y, z) && grid.IsSolid(x, y, z))
                    {
                        session.Simulation.Restore(session.Simulation.CaptureState() with { Player = spawn.Player });
                        collector.Note("the saved position is inside a block of the new world: the player starts where a new game starts");
                        return;
                    }
                }
            }
        }
    }

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    // The phrase sheet's rows, each exactly once: a save with an unknown or missing phrase is refused.
    private static void RestorePhrases(VoxelSession session, IReadOnlyList<PhraseSave> phrases, bool migrating)
    {
        var rows = session.Phrases.Rows;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phrase in phrases)
        {
            if (!rows.Any(row => string.Equals(row.Id, phrase.Id, StringComparison.Ordinal)) || !ids.Add(phrase.Id))
            {
                throw new ArgumentException($"The phrase '{phrase.Id}' is not a row of the sheet, or is given twice.", nameof(phrases));
            }
        }

        if (!migrating && ids.Count != rows.Count) // across bundles a new row has no record yet: it starts empty
        {
            throw new ArgumentException($"The save has {ids.Count} phrase records; the sheet has {rows.Count}.", nameof(phrases));
        }

        session.PhraseProgress.Restore(phrases.Select(PhraseRecordOf));
    }

    // Each NPC exactly once; a missing NPC or one the game does not have is refused.
    private static void RestoreNpcs(VoxelSession session, IReadOnlyList<NpcSave> npcs, bool migrating)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var npc in npcs)
        {
            if (!session.Data.Npcs.Ids.Contains(npc.Agent, StringComparer.Ordinal) || !seen.Add(npc.Agent))
            {
                throw new ArgumentException($"The NPC '{npc.Agent}' is not one of the game's, or is given twice.", nameof(npcs));
            }
        }

        if (!migrating && seen.Count != session.Data.Npcs.Ids.Count)
        {
            throw new ArgumentException("The save does not hold every NPC.", nameof(npcs));
        }

        foreach (var npc in npcs)
        {
            session.Conversation.RestoreAgent(npc);
        }
    }

    private static WordSave WordSaveOf(WordRecord record) => new(
        record.Lemma,
        record.Heard,
        record.Read,
        record.Spoken,
        record.SpokenCorrect,
        record.CreditUnits,
        record.CorrectTurns,
        record.LastCreditedAt,
        record.LastCreditedSession,
        record.Lapses);

    private static WordRecord RecordOf(WordSave save) => new(
        save.Lemma,
        save.Heard,
        save.Read,
        save.Spoken,
        save.SpokenCorrect,
        save.CreditUnits,
        save.CorrectTurns,
        save.LastCreditedAt,
        save.LastCreditedSession,
        save.Lapses);

    private static PhraseSave PhraseSaveOf(PhraseRecord record) => new(
        record.Id,
        record.Level,
        record.CardShown,
        record.Supported,
        record.Unaided,
        record.ChoiceOnly,
        record.DontKnow,
        record.Transfer,
        record.FailForward,
        record.Slips,
        record.Heard,
        record.Answered,
        record.StepUnits,
        record.CreditedUses,
        record.LastCreditedAt,
        record.LastCreditedSession,
        record.LastUseAt,
        record.LastUseSession);

    private static PhraseRecord PhraseRecordOf(PhraseSave save) => new(
        save.Id,
        save.Level,
        save.CardShown,
        save.Supported,
        save.Unaided,
        save.ChoiceOnly,
        save.DontKnow,
        save.Transfer,
        save.FailForward,
        save.Slips,
        save.Heard,
        save.Answered,
        save.StepUnits,
        save.CreditedUses,
        save.LastCreditedAt,
        save.LastCreditedSession,
        save.LastUseAt,
        save.LastUseSession);

    // The first characters of a hash, for a message a player can read.
    private static string Short(string hash) => hash.Length > 12 ? hash[..12] + "..." : hash;
}
