using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>
/// No player-facing English in the code (content-plan 3.2, C04): the support language's words are the locale's (<c>content/locale/</c>), so a scan of
/// <c>Vocaterra.Game</c> and <c>Vocaterra.Game.Core</c> finds no string literal that reads like an English phrase for the player. Developer text (the command
/// line, logs and console lines, exceptions, the saved report and crash files, the debug overlay) stays English and is allowed below, each file with its reason.
/// </summary>
public sealed partial class NoLiteralTextTests
{
    // The files whose literals are developer text, and the few whose player-facing text C04 leaves in the code (the open points of the plan's section 4).
    private static readonly Dictionary<string, string> AllowedFiles = new(StringComparer.Ordinal)
    {
        ["src/Vocaterra.Game/Startup/LaunchOptions.cs"] = "the command line's usage and errors",
        ["src/Vocaterra.Game/Program.cs"] = "the console at launch",
        ["src/Vocaterra.Game/VoxelGame.cs"] = "the window's title (seed and mode) and the save's reason on the console",
        ["src/Vocaterra.Game/UI/Common/UiPalette.cs"] = "the palette's colour names (the contrast test reports them)",
        ["src/Vocaterra.Game/UI/Hud/VoxelDebugText.cs"] = "the debug overlay (F3)",
        ["src/Vocaterra.Game/UI/Report/ReportScreen.cs"] = "the report file's date format",
        ["src/Vocaterra.Game/Diagnostics/CrashHandler.cs"] = "the crash file and the console",
        ["src/Vocaterra.Game/Diagnostics/MoveProbe.cs"] = "the --stream-stats line on the console (B07b)",
        ["src/Vocaterra.Game/Diagnostics/PipelineCounters.cs"] = "the --stream-stats pipeline line on the console (B43)",
        ["src/Vocaterra.Game/Streaming/NearFirstGate.cs"] = "the --stream-stats near gate line on the console (B45)",
        ["src/Vocaterra.Game/Streaming/StreamedWorldHost.cs"] = "the --stream-stats store counters and the --pregen progress line on the console (B07d)",
        ["src/Vocaterra.Game/Graphics/World/VoxelRenderer.cs"] = "the renderer's stats on the console",
        ["src/Vocaterra.Game/Startup/SoundBootstrap.cs"] = "the sound's console lines",
        ["src/Vocaterra.Game/Sound/SoundEffectOutput.cs"] = "the sound's console lines",
        ["src/Vocaterra.Game.Core/Content/GameDataValidator.cs"] = "the content authors' problem list",
        ["src/Vocaterra.Game.Core/Needs/NeedsValidator.cs"] = "the content authors' problem list (game/needs.json)",
        ["src/Vocaterra.Game.Core/Atmosphere/DayValidator.cs"] = "the content authors' problem list (game/day.json)",
        ["src/Vocaterra.Game.Core/Atmosphere/WeatherValidator.cs"] = "the content authors' problem list (game/weather.json)",
        ["src/Vocaterra.Game.Core/Temperature/TemperatureValidator.cs"] = "the content authors' problem list (game/temperature.json)",
        ["src/Vocaterra.Game.Core/Temperature/TemperatureTermRegistry.cs"] = "the content authors' problem list (game/temperature.json terms)",
        ["src/Vocaterra.Game.Core/Temperature/TermParameters.cs"] = "the content authors' problem list (a term row's parameters)",
        ["src/Vocaterra.Game.Core/Items/Gear/EquipmentValidator.cs"] = "the content authors' problem list (game/equipment.json and the gear fields of game/items.json)",
        ["src/Vocaterra.Game.Core/Damage/DamageValidator.cs"] = "the content authors' problem list (game/damage.json)",
        ["src/Vocaterra.Game.Core/Items/Wearing/WearValidator.cs"] = "the content authors' problem list (game/items.json and the repair rows of game/recipes.json)",
        ["src/Vocaterra.Game.Core/Light/PlayerLightData.cs"] = "the content authors' problem list (game/light.json)",
        ["src/Vocaterra.Game.Core/Stats/StatRule.cs"] = "the content authors' problem list (a stat outside its range, game/items.json)",
        ["src/Vocaterra.Game.Core/Land/LandValidator.cs"] = "the content authors' problem list (game/land.json)",
        ["src/Vocaterra.Game.Core/World/WorldGenValidator.cs"] = "the content authors' problem list (game/world.json)",
        ["src/Vocaterra.Game.Core/World/Biomes/BiomeValidator.cs"] = "the content authors' problem list (game/biomes.json)",
        ["src/Vocaterra.Game.Core/World/Underground/UndergroundValidator.cs"] = "the content authors' problem list (game/underground.json, B11)",
        ["src/Vocaterra.Game/Graphics/Colour/LandColours.cs"] = "the content authors' problem list (Graphics/Colour/land-colours.json, B22)",
        ["src/Vocaterra.Game.Core/World/Pass/PassValidator.cs"] = "the content authors' problem list (game/world.json, the pass section, B08b)",
        ["src/Vocaterra.Game.Core/World/Regions/RegionCatalog.cs"] = "the problem list of a set of regions (B06): the author of a region reads it, no player does",
        ["src/Vocaterra.Game.Core/World/Generation/Procedural/StrategyRegistry.cs"] = "the content authors' error for a biome row that names a strategy the code does not have (game/biomes.json)",
        ["src/Vocaterra.Game.Core/Content/GameData.cs"] = "a load error for the content authors",
        ["src/Vocaterra.Game.Core/Content/LocaleBundle.cs"] = "load errors for the content authors",
        ["src/Vocaterra.Game.Core/Language/Lessons/LessonBook.cs"] = "load errors for the content authors",
        ["src/Vocaterra.Game.Core/Language/Words/NounCatalog.cs"] = "a load error for the content authors",
        ["src/Vocaterra.Game.Core/Session/GameBundle.cs"] = "the error when the bundle is not found (a command-line run)",
        ["src/Vocaterra.Game.Core/Save/SaveGame.cs"] = "the strict-save refusal and the migration notes, printed on the console of a command-line run",
        ["src/Vocaterra.Game.Core/Save/WornSave.cs"] = "the repair lines of a load, in the session log (I10b)",
        ["src/Vocaterra.Game/Startup/GiveLine.cs"] = "the --give test aid's console line (I10b)",
        ["src/Vocaterra.Game.Core/Save/SaveMigration.cs"] = "the migration report on the console",
        ["src/Vocaterra.Game.Core/Save/Autosave.cs"] = "the autosave's reasons on the console",
        ["src/Vocaterra.Game.Core/Reporting/ReportMarkdown.cs"] = "the saved report's headings (a file for the developers)",
        ["src/Vocaterra.Game.Core/Reporting/ReportStateBuilder.cs"] = "the saved report's state rows",
        ["src/Vocaterra.Game.Core/Reporting/ReportKind.cs"] = "the kind's label in the saved report (the panel draws ui.reportBug and ui.reportFeature)",
        ["src/Vocaterra.Game.Core/Reporting/ReportPanel.cs"] = "the report state's name of the world (the panel the report was opened over)",
        ["src/Vocaterra.Game.Core/Screens/ScreenStack.cs"] = "the report state's name of the world",
        ["src/Vocaterra.Game.Core/Reporting/CrashSource.cs"] = "the crash file's rows",
        ["src/Vocaterra.Game.Core/Reporting/CrashReport.cs"] = "the crash file's title",
        ["src/Vocaterra.Game.Core/Reporting/BusyFrameStats.cs"] = "the frame stats on the console",
        ["src/Vocaterra.Game.Core/Event/LessonEvent.cs"] = "the session log's lines",
        ["src/Vocaterra.Game.Core/Event/WorldEvent.cs"] = "the session log's lines",
        ["src/Vocaterra.Game.Core/Event/VoiceEvent.cs"] = "the session log's lines",
        ["src/Vocaterra.Game.Core/Event/PhraseUse.cs"] = "the session log's lines",
        ["src/Vocaterra.Game.Core/Language/Learner/LearnerSummary.cs"] = "the console's summary at exit",
        ["src/Vocaterra.Game.Core/Language/Phrases/PhraseProgress.cs"] = "the console's summary at exit",
        ["src/Vocaterra.Game.Core/Language/Speech/SpeechLines.cs"] = "the speech files' summary on the console",
        ["src/Vocaterra.Game.Core/Language/Book/BookSpeechControls.cs"] = "the book's console lines",
        ["src/Vocaterra.Game.Core/Language/Book/BookSpeaker.cs"] = "the book's console lines",
        ["src/Vocaterra.Game.Core/Language/Checks/CheckBattery.cs"] = "the check's console lines",
        ["src/Vocaterra.Game.Core/Language/Lessons/LessonRunner.cs"] = "a step's miss and finish reasons in the session log",
        ["src/Vocaterra.Game.Core/Language/Lessons/LessonActions.cs"] = "the lesson strip's key names (the strip's keys are fixed in it)",
        ["src/Vocaterra.Game.Core/Language/Speech/MicCheckScreen.cs"] = "the screen's name in the report state",
        ["src/Vocaterra.Game.Core/Dialogue/DialogueScreen.cs"] = "the screen's name in the report state and a console line",
        ["src/Vocaterra.Game.Core/Dialogue/DialogueModel.cs"] = "the verdict notes, a developer's view that a playtest turns off",
        ["src/Vocaterra.Game.Core/Dialogue/SupportFrontend.cs"] = "the console's summary at exit",
        ["src/Vocaterra.Game.Core/Session/PlaytestFlow.cs"] = "the facilitator's console lines",
        ["src/Vocaterra.Game.Core/Session/ObjectiveAnnouncer.cs"] = "the console's objective lines",
        ["src/Vocaterra.Game.Core/Session/SessionSkip.cs"] = "the test aid's line on the console and in the session log (--skip-to-session, V12c)",
        ["src/Vocaterra.Game.Core/Scripted/CheckScript.cs"] = "the scripted runs' console",
        ["src/Vocaterra.Game/Scripted/ScriptedRunEnd.cs"] = "the scripted runs' console and session log line when a run is cut short (V26)",
        ["src/Vocaterra.Game.Core/Scripted/VoxelScript.cs"] = "the scripted runs' console",
        ["src/Vocaterra.Game/Graphics/Gl/GlInfo.cs"] = "the OpenGL path's console lines and errors (B17a, --gl-shaders)",
        ["src/Vocaterra.Game/Graphics/Gl/GlLoader.cs"] = "the OpenGL path's console lines and errors (B17a, --gl-shaders)",
        ["src/Vocaterra.Game/Graphics/Gl/GlChunkPass.cs"] = "the OpenGL path's console lines and errors (B17a, B17b, --gl-shaders)",
        ["src/Vocaterra.Game/Graphics/Gl/GlProgram.cs"] = "the OpenGL path's console lines and errors (B17a, --gl-shaders)",
        ["src/Vocaterra.Game/Graphics/Weather/PrecipitationField.cs"] = "the console line that says a precipitation kind has no style (E06)",
        ["src/Vocaterra.Game.Core/Sound/SoundMixer.cs"] = "the sound's console lines",
        ["src/Vocaterra.Game.Core/Sound/AmbientSound.cs"] = "the sound's console lines",
        ["src/Vocaterra.Game.Core/World/Generation/SceneBuilder.cs"] = "the scene's build stats on the console",
        ["src/Vocaterra.Game.Core/Session/TurnWorker.cs"] = "open point (C04): the speech problems come from the worker thread, which has no locale; and the thread's name",
        ["src/Vocaterra.Game.Core/Language/Speech/SpeechEngine.cs"] = "open point (C04): the speech models' load problems, made before the locale is at hand",
        ["src/Vocaterra.Game.Core/Input/PushToTalkSpeechCapture.cs"] = "open point (C04): the microphone's problems, made in the capture, which has no locale",
        ["src/Vocaterra.Game.Core/Language/Book/BookVerbLabels.cs"] = "open point (C04): the English verb labels are grammar rules over the locale's listed forms, not literals",
    };

    private static readonly string[] Roots = ["src/Vocaterra.Game", "src/Vocaterra.Game.Core"];

    [Fact]
    public void TheGameAndItsCoreHoldNoPlayerFacingEnglish()
    {
        var found = new List<string>();
        foreach (var file in SourceFiles())
        {
            var relative = Path.GetRelativePath(Repo.Root, file).Replace('\\', '/');
            if (AllowedFiles.ContainsKey(relative))
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                found.AddRange(PlayerFacingLiterals(line).Select(literal => $"{relative}:{number}: {literal}"));
            }
        }

        Assert.True(found.Count == 0, "Player-facing English in the code; move it to content/locale/en (ui.*):\n" + string.Join("\n", found));
    }

    [Fact]
    public void EveryAllowedFileExistsAndStillHoldsTheTextItIsAllowedFor()
    {
        // An entry for a file that is gone, or that no longer has a literal the scan would find, is dead weight: the list shrinks as the open points close.
        foreach (var (relative, reason) in AllowedFiles)
        {
            var path = Repo.Path(relative.Split('/'));
            Assert.True(File.Exists(path), $"{relative} is allowed ({reason}) but does not exist.");
            Assert.True(File.ReadLines(path).Any(line => PlayerFacingLiterals(line).Count > 0), $"{relative} is allowed ({reason}) but holds no such literal any more.");
        }
    }

    [Theory]
    [InlineData("        DrawHelpers.Text(spriteBatch, font, \"New word\", position, Gold);", true)]
    [InlineData("    public const string Title = \"Workbench\";", true)]
    [InlineData("        _toasts.Show($\"Game saved (session {session})\");", true)]
    [InlineData("        var text = ui.Ui(\"newWord\");", false)]
    [InlineData("        LogWorld(WorldEvents.ItemPickedUp, null, \"stall.give_water\");", false)]
    [InlineData("        throw new InvalidDataException(\"The file is malformed.\");", false)]
    [InlineData("        _console($\"check: {kind}, {count} items\");", false)]
    [InlineData("        var x = 1; // \"Hola.\" is what she says", false)]
    [InlineData("    /// <summary>The line \"New word\" in the toast.</summary>", false)]
    public void TheScanFindsAPhraseForThePlayerAndPassesIdsLogsAndComments(string line, bool flagged)
    {
        Assert.Equal(flagged, PlayerFacingLiterals(line).Count > 0);
    }

    // The literals of a line that read like English for a player: two words or more, or one capitalised word. A line that is a comment, an attribute or a
    // directive, or that throws, logs or writes JSON, is developer text; a literal after a // comment is not code.
    private static List<string> PlayerFacingLiterals(string line)
    {
        if (DeveloperLine().IsMatch(line))
        {
            return [];
        }

        var literals = new List<string>();
        var position = 0;
        foreach (Match match in Literal().Matches(line))
        {
            if (line.IndexOf("//", position, match.Index - position, StringComparison.Ordinal) >= 0)
            {
                break;
            }

            position = match.Index + match.Length;
            var body = Hole().Replace(match.Value.TrimStart('$', '@').Trim('"'), " ");
            if (Word().IsMatch(body) && (Phrase().IsMatch(body) || OneWord().IsMatch(body)))
            {
                literals.Add(match.Value);
            }
        }

        return literals;
    }

    private static IEnumerable<string> SourceFiles() =>
        Roots.Select(root => Repo.Path(root.Split('/')))
            .SelectMany(static root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    [GeneratedRegex(@"^\s*(///|//|\[|#)|throw |Exception\(|nameof\(|\b_?log\(|_console\(|\bconsole\(|Console\.|_error\(|WriteLine|\bLog\(|Debug\.|\.Write(String|Number|Boolean|Null|StartArray|StartObject|PropertyName)\(|GetProperty\(|TryGetProperty\(|Append\(", RegexOptions.CultureInvariant)]
    private static partial Regex DeveloperLine();

    [GeneratedRegex(@"\$?@?""(?:[^""\\\n]|\\.)*""", RegexOptions.CultureInvariant)]
    private static partial Regex Literal();

    [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex Hole();

    [GeneratedRegex(@"[A-Za-z][a-z]+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    [GeneratedRegex(@"[A-Za-z]{2,}(?:[ ,:;!?]+|\.\s+)[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Phrase();

    [GeneratedRegex(@"^\s*[A-Z][a-z]{2,}[.!?…:]?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex OneWord();
}
