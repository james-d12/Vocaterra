using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Options;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Sound;

namespace Vocaterra.Game.Startup;

/// <summary>What the start of a world made: the options the run goes on with (the seed of a loaded save is its own), the session, the built scene, the NPCs' voices and the conversation pages a save held.</summary>
/// <param name="Options">The launch options of this world.</param>
/// <param name="Session">The session.</param>
/// <param name="Scene">The built scene.</param>
/// <param name="Voice">The NPCs' voices.</param>
/// <param name="SavedDialogue">The NPCs' conversation pages a loaded save holds (U08), or <see langword="null"/> for a new game.</param>
internal sealed record StartedWorld(LaunchOptions Options, VoxelSession Session, Scene Scene, NpcVoice Voice, IReadOnlyList<DialoguePageSave>? SavedDialogue);

/// <summary>
/// The start of one world (Q01; the code that was Program's, moved so a run can start a world more than once): the scene and the session of a new game of the seed, or of a save loaded as <c>--load</c> loads it (the seed is
/// the save's unless <c>--seed</c> asked for another, the bundle must be the one it was made from or is carried into this one), the dev aids that act on the new game, and the NPCs' pre-rendered voices.
/// </summary>
internal static class WorldStart
{
    /// <summary>Builds a world from the setup the run loaded once.</summary>
    /// <param name="options">The launch options of this world.</param>
    /// <param name="setup">The bundle, locale and layout.</param>
    /// <param name="saved">The settings the settings file holds (the pregeneration reads the worker threads from them), or <see langword="null"/>.</param>
    /// <param name="startedMilliseconds">The steady clock the "loaded in" line counts from.</param>
    /// <param name="exitCode">The exit code when the run ends here (a command that has no game window, or a refused option), else 0.</param>
    /// <returns>The world, or <see langword="null"/> when the run ends with <paramref name="exitCode"/>.</returns>
    /// <exception cref="SaveException">A save to load cannot be (another seed, another bundle in strict mode, an older version, unreadable).</exception>
    public static StartedWorld? Build(LaunchOptions options, GameSetup setup, SaveSettings? saved, double startedMilliseconds, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(setup);
        exitCode = 0;
        var bundle = setup.Bundle;
        var data = setup.Data;
        var sceneStarted = MonotonicClock.Milliseconds(); // a world after the first counts its own start
        var worldMode = options.World == WorldChoice.Valley ? WorldMode.Bounded : WorldMode.Streamed; // B07: the streamed tall world by default, --world valley keeps the bounded valley
        if (!options.CheckTerrain(data.TerrainOptions, out var terrainAsked, out var terrainError))
        {
            // The options --terrain may name are the bundle's (game/world-options.json); GameSetup checked the run's own, this one is a world made from the title (Q02, Q03).
            Console.Error.WriteLine(terrainError);
            Console.Error.Write(LaunchOptions.Usage);
            exitCode = 2;
            return null;
        }

        var terrainOptions = terrainAsked ?? WorldOptions.Default; // Q02: the terrain options of the world: --terrain's, or under --load the save's (Q03 will set them from the New game page)
        Func<ulong, Scene> buildFor = seed => WorldFactory.Create(data, seed, worldMode, setup.Layout, null, terrainOptions);
        Scene scene;
        VoxelSession session;
        IReadOnlyList<DialoguePageSave>? savedDialogue = null;
        if (options.LoadPath is { } loadPath)
        {
            // A save (S10): the seed is the save's unless --seed asked for another, the bundle must be the one it was made from, and the session is the next one.
            var document = SaveGame.Parse(SaveFile.Read(loadPath));
            // A save of another bundle is refused in strict mode (--strict-save, and every --playtest: its recall check needs the frozen bundle, D-game-10); otherwise it is carried into this bundle (X01).
            SaveGame.Check(document, bundle, options.SeedGiven ? options.Seed : null, migrate: !options.StrictSave);
            var savedTerrain = SaveGame.TerrainOptionsOf(document, data);
            if (terrainAsked is not null && !terrainAsked.Equals(savedTerrain))
            {
                throw new SaveException($"The save was made with terrain options {savedTerrain}, not {terrainAsked}. Load it without --terrain, or with the save's own (--terrain {(savedTerrain.IsDefault ? "with every option at its default" : savedTerrain.Canonical.Replace(';', ','))}).");
            }

            terrainOptions = savedTerrain; // --load restores the save's terrain options with its seed
            scene = buildFor(document.WorldSeed);
            session = SaveGame.Restore(document, data, scene, options.Credit, migrate: !options.StrictSave);
            options = options with { Seed = document.WorldSeed };
            savedDialogue = document.DialoguePages;
            Console.WriteLine($"load: {loadPath}: saved in session {document.Session}, now session {session.Day}, sitting {session.Sitting}; tick {document.Tick}, stored time {document.StoredTime}");
            if (session.Migration is { } migration)
            {
                Console.WriteLine($"load: world digest {session.Simulation.ComputeStateDigest()[..16]}, the save's {document.StateDigest[..16]} ({(migration.WorldDigestMatched ? "equal" : "differs: the content changed")}); bundle {bundle.ContentHash[..12]}, the save's {document.BundleHash[..12]}");
                foreach (var line in migration.Lines())
                {
                    Console.WriteLine(line);
                }
            }
            else
            {
                Console.WriteLine($"load: world digest {session.Simulation.ComputeStateDigest()[..16]} = the save's {document.StateDigest[..16]} (restored equal); bundle {bundle.ContentHash[..12]}");
            }
        }
        else
        {
            scene = buildFor(options.Seed);
            session = new VoxelSession(data, options.Seed, GuideSession.DefaultStartedAt, scene, options.Credit);
        }

        if (options.Pregen is { } pregenRadius)
        {
            // B07d: the far view's real sections of the world round a point, made on all the workers with no window, then exit.
            exitCode = PregenCommand.Run(options, session, scene, saved, pregenRadius, Console.Out);
            return null;
        }

        if (options.SkipToSession is { } skipTo)
        {
            // A test aid (V12c): the new game stands where a learner who did sessions 1 to n-1 would, built from the data; the console, the log and the saves say so.
            var last = SessionSkip.LastSession(session.Lessons.Book);
            if (skipTo > last)
            {
                Console.Error.WriteLine($"--skip-to-session needs a play session from 2 to {last}, the last session that has a lesson; got {skipTo}.");
                Console.Error.Write(LaunchOptions.Usage);
                exitCode = 2;
                return null;
            }

            SessionSkip.Apply(session, skipTo);
            Console.WriteLine($"session: {SessionSkip.Line(skipTo)}");
        }

        if (options.OwnPlot is { } ownPlot)
        {
            // A dev aid (Y09): the plot is checked by the land rules and owned at once, with no coins and no stake; a refusal names the reason.
            var check = session.SetPendingPlot(ownPlot.X, ownPlot.Z);
            if (!check.Ok || !session.OwnPlot())
            {
                var reason = check.Ok ? "a plot is owned already" : check.Protection == Protection.None ? check.Fault.ToString() : $"{check.Fault}, {check.Protection}";
                Console.Error.WriteLine($"--own-plot {ownPlot.X},{ownPlot.Z}: the plot cannot be owned there ({reason}).");
                exitCode = 2;
                return null;
            }

            Console.WriteLine($"land: the plot {ownPlot.X},{ownPlot.Z} ({session.OwnedPlot!.Value.Width} by {session.OwnedPlot.Value.Depth}) is owned (--own-plot)");
        }

        if (options.MarkPlot is { } markPlot)
        {
            // A dev aid (Y10): the stake's use on the column, with the land rules' answer on the console and the hint line.
            var use = session.MarkPlotWithHint(markPlot.X, markPlot.Z);
            var reason = use.Check.Protection == Protection.None ? use.Check.Fault.ToString() : $"{use.Check.Fault}, {use.Check.Protection}";
            Console.WriteLine(use.Result switch
            {
                VoxelSession.StakeResult.Marked => $"land: the stake at {markPlot.X},{markPlot.Z} marked the pending plot {session.PendingPlot!.Value.X},{session.PendingPlot.Value.Z} ({session.PendingPlot.Value.Width} by {session.PendingPlot.Value.Depth}) (--mark-plot)",
                VoxelSession.StakeResult.AlreadyOwned => $"land: the stake at {markPlot.X},{markPlot.Z} marked nothing: the plot is owned already (--mark-plot)",
                _ => $"land: the stake at {markPlot.X},{markPlot.Z} was refused: {reason} (--mark-plot)",
            });
        }

        if (options.Lodging)
        {
            session.GrantLodging(); // a dev aid (Y05): the inn's bed is usable from the start
            Console.WriteLine("lodging: given (--lodging)");
        }

        if (options.Session is { } playSession)
        {
            // A run started at a given session (S26, --session): the lessons of that session are unlocked, and the objective names the new one.
            session.Day = playSession;
            session.Sitting = playSession;
            session.BeginPlaySession();
            Console.WriteLine($"session: this run is play session {playSession}");
        }

        Console.WriteLine($"scene: {setup.LayoutFile} built in {MonotonicClock.Milliseconds() - sceneStarted:0} ms");
        Console.WriteLine(terrainOptions.IsDefault ? "terrain: every option at its default" : $"terrain: {terrainOptions.Canonical.Replace(";", ", ", StringComparison.Ordinal)} (the rest at their defaults)");
        Console.WriteLine($"bundle: loaded in {MonotonicClock.Milliseconds() - startedMilliseconds:0} ms; typed credit {(options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full")}, spoken credit full");

        // The NPCs' pre-rendered voices (S9): the startup line counts the speakable lines' files; the playback never builds Kokoro.
        var speech = SpeechLines.Enumerate(bundle, session.Nouns, session.Phrases, session.Data.Npcs);
        var speechDirectory = Path.Combine(setup.Directory, SpeechLines.FolderName);
        Console.WriteLine(SpeechSummary.Of(speech, speechDirectory));
        var voice = new NpcVoice(speechDirectory, new SpeechIndex(speech), new DynamicSoundOutput(), options.Muted, Console.WriteLine);
        if (options.Muted)
        {
            Console.WriteLine("voice: muted (--mute)");
        }

        return new StartedWorld(options, session, scene, voice, savedDialogue);
    }
}
