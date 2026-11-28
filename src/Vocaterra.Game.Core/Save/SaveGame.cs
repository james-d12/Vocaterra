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
using Vocaterra.Game.Core.World.Options;
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
    /// The format version this code writes. Version 12 (Q01 and Q02) drops the player's <c>settings</c> from the save (they live in the one global settings file, <see cref="SettingsFile"/>) and adds the optional <c>terrain</c>, the world's terrain options by id and value (left out at the defaults); a save of version 11 is refused like any older one. Version 11 (I07) adds the optional <c>wear</c> of an inventory slot and of a worn item (left out at 0). Version 10 (I03b) stores the inventory's active slots only (the base and the worn bag's capacity; 30 with no bag) and adds the optional <c>worn</c> entry, the worn items by group id, slot
    /// name and item name, and, with 12, is the oldest this code reads: the game is unreleased, so a save of an older version is refused and none is migrated. (Version 9, B05, keyed the world edits by chunk coordinates
    /// (<see cref="ChunkEdit"/>: X, Y, Z, Y may be negative) instead of the dense chunk index, dropped <c>world</c> (the chunk counts) and checks the world's state digest (<see cref="WorldStateDigest"/>, the changed chunks only);
    /// I01 stored an inventory slot's item by its name in place of the code's enum name; version 8, Y02, added the optional <c>sleep</c> entry of <c>systems</c>.)
    /// </summary>
    public const int CurrentVersion = 12;

    /// <summary>The oldest format version this code reads: 12. The game is unreleased, so a save of an older version is refused and none is migrated (Q01, Q02, Y02, B05, I01, I03b).</summary>
    public const int OldestReadableVersion = 12;

    /// <summary>The terrain options of the world a save was made on (Q02), in this game's option table: the options to build the scene with before <see cref="Restore"/>.</summary>
    /// <param name="document">The save.</param>
    /// <param name="data">The game's content (its <see cref="GameData.TerrainOptions"/> rows).</param>
    /// <exception cref="SaveException">The save names an option this content lacks or a value off its grid.</exception>
    public static WorldOptions TerrainOptionsOf(SaveDocument document, GameData data)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        return TerrainOptionSave.ToOptions(document.Terrain, data.TerrainOptions);
    }

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
            throw new SaveException($"The save is format version {document.Version}; the oldest this game loads is {OldestReadableVersion} (it writes {CurrentVersion}), and older saves are not migrated.");
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
    /// no opening waiting). The world part lists the chunks that differ from the world as generated (<see cref="VoxelGridDiff.Capture"/>). The session is not changed.
    /// </summary>
    /// <param name="session">The running game.</param>
    /// <param name="dialoguePages">The NPCs' conversation pages (U08, <see cref="DialoguePageSave.Capture"/>), or <see langword="null"/> for none.</param>
    /// <exception cref="InvalidOperationException">The session is not at rest.</exception>
    public static SaveDocument Capture(VoxelSession session, IReadOnlyList<DialoguePageSave>? dialoguePages = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.CanSave)
        {
            throw new InvalidOperationException("The game is not at rest: the guide is still thinking, the key words are on show or an opening waits. Save in a moment.");
        }

        if (!session.Simulation.ReturnHeld())
        {
            throw new InvalidOperationException("A stack is held at the cursor and the bag has no room for it: put it down (the inventory screen) and save again.");
        }

        var state = session.Simulation.CaptureState(); // nothing is held now: the held stack and the trash are not in a save (I02)
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
            state.Slots.Select(slot => SlotSave.Of(slot, session.Data.Items)).ToArray(),
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
            VoxelGridDiff.Capture(session.Scene.Grid),
            session.Lessons.Capture(),
            session.Tips.Capture(),
            session.Learner.Capture(),
            BookGranted: true,
            SkippedTo: session.SkippedTo,
            Sitting: session.Sitting,
            KeyWordsTaughtBy: session.KeyWordsTaughtBy,
            Systems: new SystemsSave(session.Needs.Capture(), session.CaptureChest(), session.CaptureBushes(), session.CaptureGifts(), session.CapturePlanted(), session.CaptureProcedures(), session.CaptureSleep(), state.Atmosphere, session.CaptureLand(), session.Temperature.Capture()),
            MetNpcs: session.MetNpcs,
            Air: state.Air == Vocaterra.Game.Core.Water.Breath.MaxAir ? null : state.Air,
            Flow: state.Flow is { Count: > 0 } ? state.Flow : null,
            DialoguePages: dialoguePages is { Count: > 0 } ? dialoguePages : null,
            GivenOrders: session.GivenOrders is { Count: > 0 } given ? given : null,
            Worn: WornSave.Of(session.Simulation.Equipment),
            PassOpen: session.Pass.IsOpen && session.Pass.Locked,
            Terrain: TerrainOptionSave.Of(session.Scene.TerrainOptions));
    }

    /// <summary>Captures a session and serializes it: the text of the save file.</summary>
    /// <param name="session">The running game (at rest).</param>
    public static string Save(VoxelSession session) => Serialize(Capture(session));

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

        if (!scene.TerrainOptions.Equals(TerrainOptionsOf(document, data)))
        {
            throw new SaveException($"The scene was not built with this save's terrain options ({TerrainOptionsOf(document, data)}; the scene has {scene.TerrainOptions}).");
        }

        VoxelSession session;
        var repairs = new List<string>(); // what the load put right in the slots (an item the bundle lacks, a worn item its slot does not take); each is logged
        try
        {
            if (collector is null)
            {
                VoxelGridDiff.Apply(scene.Grid, document.Chunks);
            }
            else
            {
                SaveMigration.ApplyWorld(scene.Grid, document.Chunks, collector);
            }

            session = new VoxelSession(data, document.WorldSeed, document.StartedAt, scene, credit);
            var spawn = session.Simulation.CaptureState();
            if (collector is not null)
            {
                document = SaveMigration.Adapt(document, session, collector);
            }

            RestoreParts(session, document, migrating, repairs);
            if (collector is not null)
            {
                KeepPlayerOutOfWalls(session, spawn, collector);
            }
        }
        catch (ArgumentException ex)
        {
            throw new SaveException($"The save does not restore. {PlainMessage(ex)}");
        }

        if (session.StoredTime.UnixSeconds != document.StoredTime)
        {
            throw new SaveException($"The stored time is {session.StoredTime.UnixSeconds}, the save says {document.StoredTime}.");
        }

        var digest = session.Simulation.ComputeStateDigest();
        var digestMatched = string.Equals(digest, document.StateDigest, StringComparison.Ordinal);
        if (!digestMatched && collector is null && repairs.Count == 0)
        {
            throw new SaveException($"The restored world does not match the save's digest: the save is damaged or was made by another build (the restored digest is {digest}, the save says {document.StateDigest}).");
        }

        foreach (var repair in repairs)
        {
            // I10b, QA-2026-11-19-save-1: the digest was taken with what the load had to put right, so with a repair it is only reported, as across bundles; each repair is in the log.
            session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.SaveRepaired, null, repair));
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

        // A camp whose blocks were gone at the load gives the camp kit back, once, after the digest check too (the bag is part of the digest); a full bag leaves it owed for a later tick.
        session.ReturnOwedKit();

        // A load keeps the day as saved (V25): the objective, the orders and the lessons stand where the save left them, and only a sleep moves the day. The sitting is the
        // next one (S10), which lifts the spacing gaps; a save from before V25 has none, and its session is the sitting it continues from.
        session.Day = document.Session;
        session.Sitting = (document.Sitting ?? document.Session) + (bumpSession ? 1 : 0);
        session.SettleRest(); // the day goes on: what its objective said is not news (V12c), and the beacon is the save's

        return session;
    }

    /// <summary>
    /// The sleep at a bed or a lit fire (V12c): the running session goes on as the next day (and the next sitting, so the spacing rule sees a different play session), in the state a load of its own save would give, without a new
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
        RestoreParts(session, document, migrating: false, repairs: new List<string>()); // the needs come back as a load's do; the night's changes on them follow (ApplyNight)
        if (!collapse)
        {
            session.NoteSleptAt(); // Y02: after the restore, which brought back the save taken before this sleep; a sleep at no place and a collapse leave the last place as it was
        }

        session.Day = document.Session + 1;
        session.Sitting = (document.Sitting ?? document.Session) + 1;
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.Slept, null, string.Create(CultureInfo.InvariantCulture, $"session {document.Session} to {session.Day}"))); // the new session's first event
        session.ApplyNight(collapse);
        session.BeginPlaySession();
        session.ReturnOwedKit(); // Y12: a camp the night found gone gives its kit back
    }

    // Every part of the save, put back in the order the parts depend on: the simulation, the time, the progress, the NPCs.
    private static void RestoreParts(VoxelSession session, SaveDocument document, bool migrating, List<string> repairs)
    {
        var items = session.Data.Items;
        for (var i = 0; i < document.Inventory.Count; i++)
        {
            var slot = document.Inventory[i];
            if (!string.IsNullOrEmpty(slot.Item) && slot.Count <= 0 && !migrating)
            {
                throw new SaveException($"The save cannot be loaded: bag slot {i + 1} holds {slot.Item} but gives no count for it.");
            }

            if (!string.IsNullOrEmpty(slot.Item) && slot.Count > 0 && items.Find(slot.Item) is null)
            {
                repairs.Add($"bag slot {i + 1}: {slot.Item} is not in this bundle, the slot loads empty");
            }
        }

        var slots = document.Inventory.Select(slot => slot.ToStack(items)).ToArray();
        var load = WornSave.Resolve(document.Worn, session.Data.Equipment, items); // I03b: what the data lacks loads as nothing
        var worn = load.Stacks;
        repairs.AddRange(load.Dropped.Select(static line => line + ", the slot loads empty"));
        foreach (var (stack, where) in load.Misfits)
        {
            var room = Array.FindIndex(slots, static s => s.IsEmpty);
            if (room >= 0)
            {
                slots[room] = stack; // the fit check: an item worn where its slot does not take it goes to the bag
            }

            repairs.Add(room >= 0 ? $"{where}, moved to bag slot {room + 1}" : $"{where}, and the bag has no room for it");
        }

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
            document.Flow,
            document.Systems?.Atmosphere, // E01: a save without an entry starts the clock as a new game has it
            worn));
        session.Temperature.Restore(document.Systems?.Temperature); // I06c: the felt temperature (the band it falls in slows the walk and quickens the thirst)
        session.RestoreLand(document.Systems?.Land); // Y09: the pending plot and the owned one (the rules come from the scene)
        session.RestoreSleep(document.Systems?.Sleep); // Y02: the places made in play go back before anything that asks SleepPlaces (the objective's sleep line, the arrow)
        session.RestoreSkippedSeconds(document.SkippedSeconds);
        session.Words.Restore(document.Words.Select(RecordOf));
        RestorePhrases(session, document.Phrases, migrating);
        session.RestoreObjectiveCounts(document.Objective.Select(static step => (step.Key, step.Count)));
        session.RestoreCarded(document.Carded);
        session.RestoreMet(document.MetNpcs ?? (document.Session >= 2 ? session.Data.Npcs.Ids : [NpcService.GuideAgent])); // L01: a save from before has none
        session.RestoreGivenOrders(document.GivenOrders ?? []); // issue #54: a save without the list has taken on no order
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
            session.Learner.Restore(learner);
        }

        session.RestoreChest(document.Systems?.Chest); // N04: the chest's block is in the world's edits; here its cell and contents
        session.RestoreRenewal(document.Systems?.Bushes, document.Systems?.Gifts); // N06: the picked bushes' blocks are in the world's edits; here the cells that grow back, and the gifts given today
        session.RestorePlanted(document.Systems?.Planted); // N08: the planted bushes that stand, for the camp's cap
        session.RestoreProcedures(document.Systems?.Procedures); // W06: a save without an entry has none
        session.RestoreNeeds(document.Systems?.Needs); // N01: a save before version 7 has none, so full needs; the needs are not in the world's digest
        session.SkippedTo = document.SkippedTo; // a test run skipped to a later session stays marked (V12c)
        session.Pass.Restore(document.PassOpen); // B08b: an open pass stays open; its rockfall's blocks are in the world's edits
        session.SyncPassBarrier(); // B07: the edge wall follows the restored lock
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

    // An argument exception's message with the ".NET parameter name" suffix taken off, so a refusal reads as a sentence.
    private static string PlainMessage(ArgumentException ex)
    {
        var message = ex.Message;
        if (ex.ParamName is { Length: > 0 } name)
        {
            var suffix = $" (Parameter '{name}')";
            if (message.EndsWith(suffix, StringComparison.Ordinal))
            {
                message = message[..^suffix.Length];
            }
        }

        return message;
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
