using System.Globalization;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.Startup;

/// <summary>
/// The command line of the game (the 3D first-person scene, the prototype's only world since S4): <c>--seed &lt;ulong&gt;</c> (default 42), the
/// content bundle and the scene, and for the scripted run (<c>--screenshot &lt;path.png&gt;</c>) a list of steps in the order given: <c>--look</c>,
/// <c>--walk-to</c>, <c>--interact</c>, <c>--place</c>, <c>--select</c>, <c>--craft</c>, and, since S6, the talk steps <c>--talk &lt;npc&gt;</c> (walk to the
/// NPC, stop about two blocks away facing it, open the panel), <c>--say</c> (type and send one line to the NPC the panel is open on; with no panel
/// open it first talks to the guide, as in S3), <c>--leave</c> (close the panel) and <c>--shot &lt;png&gt;</c> (save the screen now and go on). All of them run in
/// the order given (<see cref="Script"/>); S8 adds <c>--wait &lt;seconds&gt;</c> and <c>--skip &lt;seconds&gt;</c> (the stored time moves on, so a gap passes). <c>--beacon</c> stands the beacon on a named point, <c>--journal</c>, <c>--inventory</c> and <c>--crafting</c>
/// open those screens at the end. <c>--voxel</c> is accepted and ignored (the 3D world is the only one since S4). Since S7 the scripted run also has
/// <c>--speak &lt;line&gt;</c> (synthesize the line with Kokoro and say it to the NPC as if spoken: the scripted spoken path of the demos and tests) and
/// <c>--wav &lt;file&gt;</c> (a recorded WAV said the same way), and the mic check has <c>--mic-check</c> (force it) and <c>--no-mic-check</c> (skip it).
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>The seed used when <c>--seed</c> is absent.</summary>
    public const ulong DefaultSeed = 42;

    /// <summary>The frame count used with <c>--screenshot</c> when <c>--frames</c> is absent.</summary>
    public const int DefaultScreenshotFrames = 120;

    /// <summary>The usage text printed on a command-line error or <c>--help</c>.</summary>
    public const string Usage =
        "usage: Vocaterra.Game [--seed <ulong>] [--content <dir>] [--locale <code>] [--scene <file>] [--screenshot <path.png> [--frames <n>] [--say <line>]...]\n"
        + "  --seed <ulong>          the world seed (default 42)\n"
        + "  --content <dir>         the content bundle folder (default: content/game-v1 found above the executable or the current directory)\n"
        + "  --locale <code>         the learner's support language, a locale of content/locale (default: the bundle's supportLanguage, en)\n"
        + "  --scene <file>          the scene layout (default: game/scene.json in the bundle; a file of its own skips the bundle's hash check)\n"
        + "  --screenshot <path>     run on a fixed clock, run the steps below in order, save the back buffer as a PNG and exit\n"
        + "  --frames <n>            with --screenshot: run at least n ticks before the screenshot (default 120)\n"
        + "  --look <yaw,pitch>      face this direction in degrees (yaw 0 north, 90 east; pitch positive up); a step\n"
        + "  --walk-to <x,z>         walk to this block column with Move commands, within half a block or until the tick limit; a step\n"
        + "  --interact <x,y,z>      hold the gather command on this block until it is gathered or the tick limit (aims at it); a step\n"
        + "  --place <item>@<x,y,z>  place one item (madera, antorcha, wood, torch...) in this empty cell, within reach; a step\n"
        + "  --select <n>            select a hotbar slot, 1 to 10; a step\n"
        + "  --craft <item>          craft the recipe that makes the item (antorcha) with the mesa in reach; a step\n"
        + "  --talk <npc>            walk up to the NPC (an id of game/npcs.json: guide, shopkeeper, villager, fisher, baker, smith, farmer), stop about two blocks away facing it and open the dialogue; a step\n"
        + "  --say <line>            type and send the line to the NPC the dialogue is open on (the guide when none is: walk to her and open it) and wait for the reply; a step\n"
        + "  --speak <line>          synthesize the line with Kokoro and say it to the NPC the dialogue is open on as if spoken (Vosk hears it); a step\n"
        + "  --wav <file>            say a recorded WAV file to the NPC the dialogue is open on (16-bit PCM, any rate); a step\n"
        + "  --leave                 close the dialogue (walking on or talking to another NPC closes it too); a step\n"
        + "  --wait <seconds>        let the game run this many seconds (60 ticks each) before the next step, for example to see the stall's question after the key words; a step\n"
        + "  --skip <seconds>        move the stored time on this many seconds without waiting (the demo of the spacing gaps and of the guide's review); a step\n"
        + "  --shot <path.png>       save the screen at this point of the script and go on (the final --screenshot is saved when the script is done); a step\n"
        + "  --lesson <id>           start the lesson (the guide's is `first-words`) at the NPC it is attached to: walk up, open the dialogue and begin it; a step (S16)\n"
        + "  --lesson-run <id>       run the whole lesson as a learner who gets everything right (typed answers, the fruit picked): prints every step and its outcome; a step (S17)\n"
        + "  --lesson-next           the lesson strip's Continue (explain, model, recap and a picked listen step); a step\n"
        + "  --lesson-skip           skip the lesson step in progress; a step\n"
        + "  --lesson-help           the lesson step's help path (its English hint, its model line again, the card and the English); a step\n"
        + "  --lesson-pick <n>       pick option n (1 to 4) of a listen step; a step\n"
        + "  --book-practise <id>    open the book on its Lessons page and press \"Practise again\" on the lesson (a completed one); it starts when the lesson's NPC is talked to; a step (S26)\n"
        + "  --session <n>           start the run as play session n (1 to 99), so the lessons of session n are unlocked: `--session 2` unlocks the villager's lesson `personas` (S26), `--session 3` the villager's lesson `ayer-y-manana` (S27),\n"
        + "                          `--session 4` the fisher's lesson `el-pueblo` once `ayer-y-manana` is done (`--lesson-run el-pueblo` runs it whatever came before),\n"
        + "                          `--session 5` the baker's lesson `numeros-y-pan` once `el-pueblo` is done, `--session 6` the villager's lesson `gente-del-pueblo` once\n"
        + "                          `numeros-y-pan` is done, and so on to `--session 10` (`como-es`, `ayer-en-el-pueblo`, `manana-en-el-pueblo`, `el-mercado`, each after the one before)\n"
        + "  --skip-to-session <n>   a test aid: start a new game as play session n (2 up to the last session that has a lesson) as a learner who did everything before it:\n"
        + "                          the earlier sessions' lessons completed, session 1's camp done and its fire lit, the earlier orders delivered and paid, no practice counted;\n"
        + "                          the console, the session log and the saves say so. Not with --load, --session or --playtest (V12c)\n"
        + "  --sleep                 sleep at the player's own fire now, as E there does, wherever the player stands: the next play session begins as a load of the save would\n"
        + "                          begin it (the fire must be lit, no lesson step waiting; not in a playtest); a step (V12c)\n"
        + "  --tips                  draw the first-time tips in a scripted run too (an interactive run always does; S16)\n"
        + "  --third-person          start in the behind-the-shoulder view (S15; F5 switches between it and first person)\n"
        + "  --dusk                  a lower, dimmer sun and lanterns that glow at full strength (S15)\n"
        + "  --beacon <point>        stand the beacon column on a named point of the scene (camp.guide, camp.player, firering, workbench, stall, signpost, village, house1, bridge, river, lake,\n"
        + "                          waterfall...); the default is camp.guide (the walk down the trail), none puts no beacon up\n"
        + "  --journal               open the journal once the steps are done\n"
        + "  --journal-phrases       open the journal on its phrases page once the steps are done (Tab turns the page)\n"
        + "  --book                  open the book (J) once the steps are done: the same as --journal\n"
        + "  --book-page <page>      open the book on nouns, verbs, phrases, lessons or said (What I said) once the steps are done\n"
        + "  --size <W>x<H>          the window's size (default 1280x720; 800x600 is the smallest the book is made for)\n"
        + "  --text-scale <n>        the size of the interface text in percent: 100, 125 or 150 (default: the save's, else 100; F9 steps through them; X08)\n"
        + "  --book-verb <verb>     open the book on the verbs page with this verb's conjugation table on the right page (S28)\n"
        + "  --book-detail <lines>  scroll the book's right page down by this many lines when it opens (a scripted run, X03)\n"
        + "  --inventory             open the inventory screen once the steps are done\n"
        + "  --crafting              open the crafting panel once the steps are done\n"
        + "  --mic-check             show the mic check at the start (an interactive run shows it by default; a scripted run with --screenshot never does unless asked,\n"
        + "                          and then hears a synthesized \"hola\" when the machine has no microphone)\n"
        + "  --no-mic-check          never show the mic check\n"
        + "  --save <file>           write the game to this save file when the run ends (on quit, or at the end of a script); F6 writes it in an interactive run\n"
        + "  --load <file>           start from a save file: the same world, inventory, objective and progress, in the next play session (the session counter\n"
        + "                          shows it); refused when the save is for another seed (with --seed) or another content bundle\n"
        + "  --check <pre|post|recall>  run the check battery (S11) first, outside the world: pre then the world; post or recall instead of the world (the run ends\n"
        + "                          after it). Its items are answered by the player, or by the script's --pick and --say/--speak steps\n"
        + "  --pick <n>              answer the check's comprehension item with the option n (1 to 3, as shown); a step, with --check\n"
        + "  --no-autosave           do not autosave (X05): the autosave in the session folder is not written; a playtest's playtest.save is still written at its end\n"
        + "  --session-dir <dir>     write this run's session.jsonl and replay.jsonl here (default: a new folder under ~/.local/share/vocaterra/sessions/)\n"
        + "  --report <text>         open the report panel (F8), type the text and save the report (a Markdown file and a screenshot); a step (S21)\n"
        + "  --report-open <text>   open the report panel, type the text and leave the panel open (the screenshot of the panel); a step (S21)\n"
        + "  --report-kind <bug|feature>  the kind a report starts with (default bug; S21)\n"
        + "  --report-dir <dir>      write the reports here instead of the session folder's reports/ (S21)\n"
        + "  --typed-credit <full|half> the credit a correct typed use earns (default half; spoken input always earns full)\n"
        + "  --no-speech             do not load the speech models (typing only)\n"
        + "  --mute                  the NPCs' pre-rendered voices are off (the panel still shows every line)\n"
        + "  --sfx-volume <0..1>     the volume of the effects (gathering, placing, picking up, crafting, the fire); default 0.8, or the save's (S24)\n"
        + "  --ambient-volume <0..1> the volume of the ambient beds; default 0.6, or the save's (S24)\n"
        + "  --no-sounds             every sound but the NPCs' voices is off: effects, interface and progress sounds, beeps and ambient (S24)\n"
        + "  --sound-log <file>      write every cue played, with its tick and category, to this file; a scripted run plays nothing aloud (S24)\n"
        + "  --quality <low|medium|high>  the graphics quality (S13): how far the world is drawn (low 224 blocks, medium 304, high 400, the default); the fog closes in at that distance\n"
        + "  --underwater-blur <off|low|medium|high>  how much the view softens under water (H05; default low)\n"
        + "  --view-distance <blocks>     draw this far instead of the quality's distance (at least 48)\n"
        + "  --pour <x,y,z>          put a water source in this cell, which then flows (a debug step for screenshots, H06); a step\n"
        + "  --teleport <x,z>        put the player at this block column, standing on the ground (a debug step for screenshots); a step\n"
        + "  --greyscale             the playtest A/B switch (D-game-6): blocks whose noun is not known yet are grey, known ones in colour; default full colour\n"
        + "  --assume-known <nouns>  a test aid: the comma-separated catalogue nouns count as known from the start (their blocks are in colour)\n"
        + "  --strict-save           refuse a save made from another content bundle (the playtest's frozen bundle, D-game-10); without it a --load across bundles keeps\n"
     + "                          the progress where its ids still exist and drops the rest (X01); --playtest implies it\n"
        + "  --playtest              the playtest profile (S12): no debug overlay, the mic check, the session log in its own folder, the pre-check before the\n"
        + "                          world (a fresh start) or the recall check (with --load), the post-check when the camp is done and the transfer offered\n"
        + "                          (or F10 ends the run), then playtest.save in the session folder and the thank-you screen until Esc; --greyscale stays\n"
        + "                          the A/B switch; --check, --no-mic-check and --session-dir still apply\n"
        + "  --voxel                 accepted and ignored (the 3D world is the only world)\n"
        + "Controls: WASD or the arrows walk, Space jumps, the mouse looks; hold the left button to gather, the right to place; E opens the inventory (or\n"
        + "  the crafting panel at the workbench, or talks to the guide when the crosshair is on her); C the crafting panel; J the journal; 1..0 the hotbar.\n"
        + "Push-to-talk: hold V or Space while the dialogue is open, or hold V with the crosshair on an NPC to open it and listen. In the dialogue: R repeat,\n"
        + "  S slower, H help, N \"no sé\", W or Tab type (Tab or Esc back to the buttons), 1 to 3 pick an offered choice, Esc cancels a turn that is thinking, then closes.\n"
        + "F8 reports a bug or asks for a feature: a Markdown file and a screenshot in the session folder (Ctrl+Enter saves, Esc cancels).\n"
        + "F1 shows the controls in English. In a lesson at the guide: L starts it, Enter continues, K skips the step, H helps, 1 to 4 pick an answer.\n";

    /// <summary>The world seed.</summary>
    public ulong Seed { get; init; } = DefaultSeed;

    /// <summary>Whether <c>--seed</c> was given: a load then refuses a save of another seed, and without it the save's seed is taken.</summary>
    public bool SeedGiven { get; init; }

    /// <summary>The save file to write (on quit, on F6 and at the end of a scripted run), or <see langword="null"/>.</summary>
    public string? SavePath { get; init; }

    /// <summary>The save file to load into the new session (S10), or <see langword="null"/> to start a new game.</summary>
    public string? LoadPath { get; init; }

    /// <summary>The check battery to run (<c>--check</c>), or <see langword="null"/> for none.</summary>
    public CheckKind? Check { get; init; }

    /// <summary>Whether <c>--no-autosave</c> was given: the game writes no autosave (X05).</summary>
    public bool NoAutosave { get; init; }

    /// <summary>The tick at which the game throws on purpose (<c>--debug-crash-after</c>, a test-only hook of X05, not in the usage), or <see langword="null"/>.</summary>
    public long? DebugCrashAfter { get; init; }

    /// <summary>The folder the session log is written to (<c>--session-dir</c>), or <see langword="null"/> for a new folder under the sessions folder.</summary>
    public string? SessionDir { get; init; }

    /// <summary>The folder the reports are written to (<c>--report-dir</c>), or <see langword="null"/> for <c>reports/</c> in the session folder (S21).</summary>
    public string? ReportDir { get; init; }

    /// <summary>The kind a report starts with (<c>--report-kind</c>): Bug unless given (S21).</summary>
    public ReportKind ReportKind { get; init; } = ReportKind.Bug;

    /// <summary>The content bundle folder, or <see langword="null"/> to search for <c>content/game-v1</c>.</summary>
    public string? ContentDirectory { get; init; }

    /// <summary>The support-language locale named by <c>--locale</c> (content-plan C03), or <see langword="null"/> for the bundle's <c>supportLanguage</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>The scene layout file named by <c>--scene</c>, or <see langword="null"/> for the bundle's <c>game/scene.json</c>.</summary>
    public string? SceneFile { get; init; }

    /// <summary>Where to save the screenshot, or <see langword="null"/> for an interactive run.</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>The ticks to run before the screenshot (only used with <see cref="ScreenshotPath"/>).</summary>
    public int Frames { get; init; } = DefaultScreenshotFrames;

    /// <summary>The lines the scripted run says (<c>--say</c>), in order.</summary>
    public IReadOnlyList<string> Says { get; init; } = [];

    /// <summary>The body's scripted steps (<c>--look</c>, <c>--walk-to</c>, <c>--interact</c>, <c>--place</c>, <c>--select</c>, <c>--craft</c>) in the order given.</summary>
    public IReadOnlyList<VoxelStep> VoxelSteps { get; init; } = [];

    /// <summary>Every step of the scripted run in the order given: the body's steps, <c>--talk</c>, <c>--say</c>, <c>--leave</c> and <c>--shot</c>.</summary>
    public IReadOnlyList<ScriptStep> Script { get; init; } = [];

    /// <summary>
    /// Checks the NPCs the <c>--talk</c> steps name against the bundle's NPCs, once the roster has loaded (village-plan V01; a fixed list of three before): a fourth NPC
    /// in <c>game/npcs.json</c> can be talked to with no code change.
    /// </summary>
    /// <param name="npcs">The NPCs' ids, in the roster's order.</param>
    /// <param name="error">The message for the first unknown NPC, or empty.</param>
    /// <returns>Whether every <c>--talk</c> names one of the NPCs.</returns>
    public bool CheckTalkTargets(IReadOnlyList<string> npcs, out string error)
    {
        ArgumentNullException.ThrowIfNull(npcs);
        var unknown = Script.FirstOrDefault(step => step.Kind == ScriptStepKind.Talk && !npcs.Contains(step.Value, StringComparer.Ordinal));
        error = unknown is null ? string.Empty : $"--talk needs an NPC ({string.Join(", ", npcs)}), got '{unknown.Value}'.";
        return unknown is null;
    }

    /// <summary>The credit a correct use earns per kind of input (<c>--typed-credit</c>); typed input earns half by default.</summary>
    public CreditRules Credit { get; init; } = CreditRules.Default;

    /// <summary>Whether <c>--no-speech</c> was given: Vosk is not loaded.</summary>
    public bool NoSpeech { get; init; }

    /// <summary>Whether the NPCs' voices are off (<c>--mute</c>): the panel shows the lines and nothing plays (S9).</summary>
    public bool Muted { get; init; }

    /// <summary>The effects' volume asked for with <c>--sfx-volume</c> (0 to 1), or <see langword="null"/> to take the save's or the default (S24).</summary>
    public double? SfxVolume { get; init; }

    /// <summary>The ambient beds' volume asked for with <c>--ambient-volume</c> (0 to 1), or <see langword="null"/> to take the save's or the default (S24).</summary>
    public double? AmbientVolume { get; init; }

    /// <summary>Whether <c>--no-sounds</c> was given: every sound but the NPCs' voices is off (S24).</summary>
    public bool NoSounds { get; init; }

    /// <summary>The file <c>--sound-log</c> names: a text log of every cue played, with its tick and category (S24), or <see langword="null"/>.</summary>
    public string? SoundLogPath { get; init; }

    /// <summary>Whether the journal is opened once the script is done (<c>--journal</c>, or <c>--journal-phrases</c>).</summary>
    public bool Journal { get; init; }

    /// <summary>Whether the journal opens on its phrases page (<c>--journal-phrases</c>, S8); otherwise on its words page.</summary>
    public bool JournalPhrases { get; init; }

    /// <summary>The play session the run starts as (<c>--session</c>, S26), or <see langword="null"/> for the game's own (1 for a new game, the save's plus one for a load).</summary>
    public int? Session { get; init; }

    /// <summary>
    /// The play session a new game is skipped to (<c>--skip-to-session</c>, V12c, a test aid: the earlier sessions counted as done, <c>SessionSkip</c>), or <see langword="null"/>.
    /// The bundle's last session that has a lesson is checked once the content has loaded.
    /// </summary>
    public int? SkipToSession { get; init; }

    /// <summary>The page the book opens on in a scripted run (<c>--book-page</c>, or the phrases page for <c>--journal-phrases</c>); <see langword="null"/> for the nouns page (S20).</summary>
    public BookPage? BookStart { get; init; }

    /// <summary>The verb whose table the book opens on, with the verbs page (<c>--book-verb</c>, S28), or <see langword="null"/>.</summary>
    public string? BookVerb { get; init; }

    /// <summary>The lines the right page is scrolled down when the book opens (<c>--book-detail</c>, X03), so a scripted run can show a long block scrolled; 0 for none.</summary>
    public int BookDetailLines { get; init; }

    /// <summary>The window's width in pixels (<c>--size</c>, S28), or <see langword="null"/> for the default.</summary>
    public int? WindowWidth { get; init; }

    /// <summary>The window's height in pixels (<c>--size</c>, S28), or <see langword="null"/> for the default.</summary>
    public int? WindowHeight { get; init; }

    /// <summary>The interface text size in percent (<c>--text-scale</c>, X08): 100, 125 or 150; <see langword="null"/> to take the save's, else 100.</summary>
    public int? TextScale { get; init; }

    /// <summary>Whether the inventory screen is opened once the script is done (<c>--inventory</c>).</summary>
    public bool ShowInventory { get; init; }

    /// <summary>Whether the crafting panel is opened once the script is done (<c>--crafting</c>).</summary>
    public bool ShowCrafting { get; init; }

    /// <summary>The named point of the scene the beacon starts on (<c>--beacon</c>), or <see langword="null"/>.</summary>
    public string? Beacon { get; init; }

    /// <summary>The graphics quality (<c>--quality</c>); <see cref="QualityLevel.High"/> by default.</summary>
    public QualityLevel Quality { get; init; } = QualityLevel.High;

    /// <summary>How much the view softens under water (<c>--underwater-blur</c>, H05); <see cref="UnderwaterBlurSettings.Default"/> (low) by default.</summary>
    public UnderwaterBlur UnderwaterBlur { get; init; } = UnderwaterBlurSettings.Default;

    /// <summary>The view distance in blocks given by <c>--view-distance</c>, or <see langword="null"/> for the quality's own.</summary>
    public int? ViewDistance { get; init; }

    /// <summary>The preset the run uses: the quality's, with the view distance <c>--view-distance</c> gave.</summary>
    public QualityPreset QualityPreset => ViewDistance is { } blocks ? QualityPreset.For(Quality).WithViewDistance(blocks) : QualityPreset.For(Quality);

    /// <summary>Whether <c>--greyscale</c> was given: blocks whose noun is not known are drawn grey (the playtest A/B, D-game-6).</summary>
    public bool Greyscale { get; init; }

    /// <summary>Whether <c>--tips</c> was given: a scripted run draws the first-time tips too (S16).</summary>
    public bool Tips { get; init; }

    /// <summary>Whether <c>--third-person</c> was given: the run starts in the behind-the-shoulder view (S15; F5 flips it in an interactive run).</summary>
    public bool ThirdPerson { get; init; }

    /// <summary>Whether <c>--dusk</c> was given (S15): the sun is lower and dimmer and the lanterns glow at full strength.</summary>
    public bool Dusk { get; init; }

    /// <summary>Whether the first-time tips are drawn: an interactive run always shows them, a scripted run only with <c>--tips</c>.</summary>
    public bool ShowsTips => !IsScreenshotRun || Tips;

    /// <summary>The catalogue nouns that count as known from the start (<c>--assume-known</c>, a test aid); empty by default.</summary>
    public IReadOnlyList<string> AssumeKnown { get; init; } = [];

    /// <summary>Whether the mic check is forced (<c>--mic-check</c>), skipped (<c>--no-mic-check</c>) or left to the default.</summary>
    public MicCheckMode MicCheck { get; init; } = MicCheckMode.Auto;

    /// <summary>
    /// Whether the run is the playtest profile (<c>--playtest</c>, S12): no debug overlay, the mic check on, the pre-check before the world (or the
    /// recall check on a load), the post-check at the end, a save in the session folder and a thank-you screen.
    /// </summary>
    public bool Playtest { get; init; }

    /// <summary>
    /// Whether a save of another content bundle is refused (<c>--strict-save</c>, or <c>--playtest</c>, X01). Off, a <c>--load</c> across bundles keeps the progress where its ids still
    /// exist and logs what was dropped. A playtest is strict because its recall check needs the bundle frozen between the sessions (D-game-10).
    /// </summary>
    public bool StrictSave { get; init; }

    /// <summary>Whether the run is the next-day recall of a playtest (<c>--playtest --load</c>): it runs the recall check and saves nothing.</summary>
    public bool IsPlaytestRecall => Playtest && Check == CheckKind.Recall;

    /// <summary>Whether the run is the scripted screenshot run.</summary>
    public bool IsScreenshotRun => ScreenshotPath is not null;

    /// <summary>The options the sound side's start-up reads (<see cref="SoundSetup"/>).</summary>
    public SoundOptions Sound => new(IsScreenshotRun, NoSounds, SfxVolume, AmbientVolume);

    /// <summary>Whether the script has a spoken step (<c>--speak</c> or <c>--wav</c>): it needs Vosk, and Kokoro for <c>--speak</c>.</summary>
    public bool HasSpokenSteps => Script.Any(static step => step.Kind is ScriptStepKind.Speak or ScriptStepKind.Wav);

    /// <summary>
    /// Whether the run loads the speech models: an interactive run does, unless <c>--no-speech</c>; a scripted run does only when it speaks
    /// (<c>--speak</c>, <c>--wav</c>) or checks the microphone (<c>--mic-check</c>).
    /// </summary>
    public bool NeedsSpeech => !NoSpeech && (!IsScreenshotRun || HasSpokenSteps || MicCheck == MicCheckMode.Force);

    /// <summary>Whether the run records from the real microphone (an interactive run that loads speech).</summary>
    public bool UsesMicrophone => NeedsSpeech && !IsScreenshotRun;

    /// <summary>
    /// Whether the mic check screen is shown at the start (D-game-9): <c>--mic-check</c> always (it needs the speech models), <c>--no-mic-check</c>
    /// never, otherwise in an interactive run that loads speech and in no scripted run.
    /// </summary>
    public bool ShowsMicCheck => MicCheck switch
    {
        MicCheckMode.Force => !NoSpeech,
        MicCheckMode.Skip => false,
        _ => NeedsSpeech && !IsScreenshotRun,
    };

    /// <summary>Parses the arguments. On failure <paramref name="error"/> says why (or is empty for <c>--help</c>) and the options are defaults.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out LaunchOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(args);
        var result = new LaunchOptions();
        options = result;
        string? screenshot = null;
        var framesGiven = false;
        var says = new List<string>();
        var steps = new List<VoxelStep>();
        var script = new List<ScriptStep>();
        string? beacon = null;
        CheckKind? check = null;
        var journal = false;
        var journalPhrases = false;
        BookPage? bookPage = null;
        string? bookVerb = null;
        var bookDetailLines = 0;
        int? windowWidth = null;
        int? windowHeight = null;
        int? textScale = null;
        var inventory = false;
        var crafting = false;
        var greyscale = false;
        var micCheck = MicCheckMode.Auto;
        var playtest = false;
        var strictSave = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                error = string.Empty;
                return false;
            }

            if (arg is "--journal" or "--journal-phrases" or "--book" or "--inventory" or "--crafting" or "--no-speech" or "--mute" or "--no-sounds" or "--voxel" or "--greyscale" or "--strict-save" or "--leave" or "--mic-check" or "--no-mic-check"
                    or "--playtest" or "--no-autosave" or "--tips" or "--third-person" or "--dusk" or "--lesson-next" or "--lesson-skip" or "--lesson-help" or "--sleep")
            {
                switch (arg)
                {
                    case "--leave":
                        script.Add(new ScriptStep(ScriptStepKind.Leave));
                        break;
                    case "--sleep":
                        script.Add(new ScriptStep(ScriptStepKind.Sleep));
                        break;
                    case "--tips":
                        result = result with { Tips = true };
                        break;
                    case "--third-person":
                        result = result with { ThirdPerson = true };
                        break;
                    case "--dusk":
                        result = result with { Dusk = true };
                        break;
                    case "--lesson-next":
                        script.Add(new ScriptStep(ScriptStepKind.LessonContinue));
                        break;
                    case "--lesson-skip":
                        script.Add(new ScriptStep(ScriptStepKind.LessonSkip));
                        break;
                    case "--lesson-help":
                        script.Add(new ScriptStep(ScriptStepKind.LessonHelp));
                        break;
                    case "--mute":
                        result = result with { Muted = true };
                        break;
                    case "--no-sounds":
                        result = result with { NoSounds = true };
                        break;
                    case "--mic-check":
                        micCheck = MicCheckMode.Force;
                        break;
                    case "--no-mic-check":
                        micCheck = MicCheckMode.Skip;
                        break;
                    case "--journal":
                    case "--book":
                        journal = true;
                        break;
                    case "--journal-phrases":
                        journal = true;
                        journalPhrases = true;
                        break;
                    case "--inventory":
                        inventory = true;
                        break;
                    case "--crafting":
                        crafting = true;
                        break;
                    case "--no-speech":
                        result = result with { NoSpeech = true };
                        break;
                    case "--greyscale":
                        greyscale = true;
                        break;
                    case "--playtest":
                        playtest = true;
                        break;
                    case "--strict-save":
                        strictSave = true;
                        break;
                    case "--no-autosave":
                        result = result with { NoAutosave = true };
                        break;
                    default:
                        break; // --voxel: accepted for old scripts, the 3D world is the only one
                }

                continue;
            }

            if (arg is not ("--seed" or "--save" or "--load" or "--screenshot" or "--frames" or "--say" or "--content" or "--locale" or "--scene" or "--look" or "--walk-to" or "--interact"
                or "--place" or "--pour" or "--select" or "--craft" or "--beacon" or "--typed-credit" or "--assume-known" or "--talk" or "--shot" or "--speak" or "--wav" or "--wait" or "--skip"
                or "--check" or "--pick" or "--session-dir" or "--session" or "--skip-to-session" or "--book-practise" or "--lesson" or "--lesson-run" or "--lesson-pick" or "--quality" or "--underwater-blur" or "--view-distance" or "--teleport" or "--book-page" or "--book-verb" or "--book-detail" or "--size" or "--text-scale"
                or "--report" or "--report-open" or "--report-kind" or "--report-dir" or "--sfx-volume" or "--ambient-volume" or "--sound-log"
                or "--book-play" or "--book-play-slow" or "--debug-crash-after"))
            {
                error = $"Unknown argument '{arg}'.";
                return false;
            }

            if (i + 1 >= args.Count)
            {
                error = $"{arg} needs a value.";
                return false;
            }

            var value = args[++i];
            switch (arg)
            {
                case "--seed":
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
                    {
                        error = $"--seed needs an unsigned integer, got '{value}'.";
                        return false;
                    }

                    result = result with { Seed = seed, SeedGiven = true };
                    break;
                case "--session":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var playSession) || playSession is < 1 or > 99)
                    {
                        error = $"--session needs a play session from 1 to 99, got '{value}'.";
                        return false;
                    }

                    result = result with { Session = playSession };
                    break;
                case "--skip-to-session":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var skipTo) || skipTo is < 2 or > 99)
                    {
                        error = $"--skip-to-session needs a play session from 2 (up to the last session that has a lesson), got '{value}'.";
                        return false;
                    }

                    result = result with { SkipToSession = skipTo };
                    break;
                case "--book-practise":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        error = "--book-practise needs the id of a completed lesson (the guide's is 'first-words').";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.BookPractise, Value: value.Trim()));
                    break;
                case "--save":
                    if (value.Length == 0)
                    {
                        error = "--save needs a file path.";
                        return false;
                    }

                    result = result with { SavePath = value };
                    break;
                case "--check":
                    check = CheckKinds.Parse(value);
                    if (check is null)
                    {
                        error = $"--check needs pre, post or recall, got '{value}'.";
                        return false;
                    }

                    break;
                case "--report":
                    if (value.Length == 0)
                    {
                        error = "--report needs the text of the report.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Report, Value: value));
                    break;
                case "--report-open":
                    script.Add(new ScriptStep(ScriptStepKind.ReportOpen, Value: value));
                    break;
                case "--report-kind":
                    if (!ReportKinds.TryParse(value, out var reportKind))
                    {
                        error = $"--report-kind needs bug or feature, got '{value}'.";
                        return false;
                    }

                    result = result with { ReportKind = reportKind };
                    break;
                case "--sfx-volume":
                case "--ambient-volume":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var volume) || double.IsNaN(volume) || volume < 0 || volume > 1)
                    {
                        error = $"{arg} needs a number from 0 to 1, got '{value}'.";
                        return false;
                    }

                    result = arg == "--sfx-volume" ? result with { SfxVolume = volume } : result with { AmbientVolume = volume };
                    break;
                case "--sound-log":
                    if (value.Length == 0)
                    {
                        error = "--sound-log needs a file path.";
                        return false;
                    }

                    result = result with { SoundLogPath = value };
                    break;
                case "--report-dir":
                    if (value.Length == 0)
                    {
                        error = "--report-dir needs a folder.";
                        return false;
                    }

                    result = result with { ReportDir = value };
                    break;
                case "--debug-crash-after":
                    // A test hook (X05, not in the usage): the game throws on the game loop once the simulation reaches this tick, so the crash handler runs.
                    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var crashTick) || crashTick < 0)
                    {
                        error = $"--debug-crash-after needs a tick (0 or more), got '{value}'.";
                        return false;
                    }

                    result = result with { DebugCrashAfter = crashTick };
                    break;
                case "--session-dir":
                    if (value.Length == 0)
                    {
                        error = "--session-dir needs a folder.";
                        return false;
                    }

                    result = result with { SessionDir = value };
                    break;
                case "--pick":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pick) || pick < 1 || pick > 3)
                    {
                        error = $"--pick needs an option from 1 to 3, got '{value}'.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Pick, Value: pick.ToString(CultureInfo.InvariantCulture)));
                    break;
                case "--load":
                    if (value.Length == 0)
                    {
                        error = "--load needs a file path.";
                        return false;
                    }

                    result = result with { LoadPath = value };
                    break;
                case "--screenshot":
                    if (value.Length == 0)
                    {
                        error = "--screenshot needs a file path.";
                        return false;
                    }

                    screenshot = value;
                    break;
                case "--frames":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var frames) || frames < 1)
                    {
                        error = $"--frames needs a positive integer, got '{value}'.";
                        return false;
                    }

                    framesGiven = true;
                    result = result with { Frames = frames };
                    break;
                case "--say":
                    if (value.Trim().Length == 0)
                    {
                        error = "--say needs a line to say.";
                        return false;
                    }

                    says.Add(value);
                    script.Add(new ScriptStep(ScriptStepKind.Say, Value: value));
                    break;
                case "--speak":
                    if (value.Trim().Length == 0)
                    {
                        error = "--speak needs a line to speak.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Speak, Value: value));
                    break;
                case "--wav":
                    if (value.Length == 0)
                    {
                        error = "--wav needs a WAV file.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Wav, Value: value));
                    break;
                case "--book-play":
                case "--book-play-slow":
                    if (value.Trim().Length == 0)
                    {
                        error = $"{arg} needs a noun (like madera) or a phrase (like phrase:pedir-agua), got '{value}'.";
                        return false;
                    }

                    script.Add(new ScriptStep(arg == "--book-play" ? ScriptStepKind.BookPlay : ScriptStepKind.BookPlaySlow, Value: value.Trim()));
                    break;
                case "--talk":
                    // Which NPCs there are is the bundle's (game/npcs.json): CheckTalkTargets refuses an unknown one once the roster has loaded (village-plan V01).
                    if (value.Trim().Length == 0)
                    {
                        error = $"--talk needs an NPC, got '{value}'.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Talk, Value: value.Trim()));
                    break;
                case "--wait":
                case "--skip":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount < 1 || amount > 86_400)
                    {
                        error = $"{arg} needs a whole number of seconds from 1 to 86400, got '{value}'.";
                        return false;
                    }

                    script.Add(new ScriptStep(arg == "--wait" ? ScriptStepKind.Wait : ScriptStepKind.Skip, Value: amount.ToString(CultureInfo.InvariantCulture)));
                    break;
                case "--lesson":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        error = "--lesson needs the id of a lesson (the guide's is 'first-words').";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.LessonStart, Value: value.Trim()));
                    break;
                case "--lesson-run":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        error = "--lesson-run needs the id of a lesson (the guide's is 'first-words').";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.LessonRun, Value: value.Trim()));
                    break;
                case "--lesson-pick":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var option) || option is < 1 or > 4)
                    {
                        error = $"--lesson-pick needs an option from 1 to 4, got '{value}'.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.LessonPick, Value: option.ToString(CultureInfo.InvariantCulture)));
                    break;
                case "--shot":
                    if (value.Length == 0)
                    {
                        error = "--shot needs a file path.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Shot, Value: value));
                    break;
                case "--content":
                    if (value.Length == 0)
                    {
                        error = "--content needs a folder.";
                        return false;
                    }

                    result = result with { ContentDirectory = value };
                    break;
                case "--locale":
                    if (value.Length == 0)
                    {
                        error = "--locale needs a locale code (en).";
                        return false;
                    }

                    result = result with { Locale = value };
                    break;
                case "--scene":
                    if (value.Length == 0)
                    {
                        error = "--scene needs a layout file.";
                        return false;
                    }

                    result = result with { SceneFile = value };
                    break;
                case "--quality":
                    if (QualityPreset.Parse(value) is not { } quality)
                    {
                        error = $"--quality needs low, medium or high, got '{value}'.";
                        return false;
                    }

                    result = result with { Quality = quality };
                    break;
                case "--underwater-blur":
                    if (UnderwaterBlurSettings.Parse(value) is not { } underwaterBlur)
                    {
                        error = $"--underwater-blur needs off, low, medium or high, got '{value}'.";
                        return false;
                    }

                    result = result with { UnderwaterBlur = underwaterBlur };
                    break;
                case "--view-distance":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var viewDistance) || viewDistance < QualityPreset.MinimumViewDistance)
                    {
                        error = $"--view-distance needs a number of blocks, at least {QualityPreset.MinimumViewDistance}; got '{value}'.";
                        return false;
                    }

                    result = result with { ViewDistance = viewDistance };
                    break;
                case "--teleport":
                    if (!VoxelStep.TryParseColumn(value, out var teleportX, out var teleportZ))
                    {
                        error = $"--teleport needs a block column and row, like 230,222; got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.Teleport(teleportX, teleportZ));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--size":
                    var sizeParts = value.Trim().ToLowerInvariant().Split('x');
                    if (sizeParts.Length != 2 || !int.TryParse(sizeParts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var sizeW)
                        || !int.TryParse(sizeParts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var sizeH) || sizeW < 640 || sizeH < 480 || sizeW > 3840 || sizeH > 2160)
                    {
                        error = $"--size needs <width>x<height> from 640x480 to 3840x2160, got '{value}'.";
                        return false;
                    }

                    windowWidth = sizeW;
                    windowHeight = sizeH;
                    break;
                case "--text-scale":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var scalePercent) || !SaveSettings.TextScaleLevels.Contains(scalePercent))
                    {
                        error = $"--text-scale needs one of {string.Join(", ", SaveSettings.TextScaleLevels)} (percent), got '{value}'.";
                        return false;
                    }

                    textScale = scalePercent;
                    break;
                case "--book-detail":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var detailLines) || detailLines < 1)
                    {
                        error = $"--book-detail needs a positive number of lines, got '{value}'.";
                        return false;
                    }

                    bookDetailLines = detailLines;
                    break;
                case "--book-verb":
                    bookVerb = value.Trim().ToLowerInvariant();
                    bookPage = BookPage.Verbs;
                    journal = true;
                    break;
                case "--book-page":
                    bookPage = value.Trim().ToLowerInvariant() switch
                    {
                        "nouns" => BookPage.Nouns,
                        "verbs" => BookPage.Verbs,
                        "phrases" => BookPage.Phrases,
                        "lessons" => BookPage.Lessons,
                        "said" => BookPage.Said,
                        _ => null,
                    };
                    if (bookPage is null)
                    {
                        error = $"--book-page needs nouns, verbs, phrases, lessons or said, got '{value}'.";
                        return false;
                    }

                    journal = true;
                    break;
                case "--beacon":
                    if (value.Trim().Length == 0)
                    {
                        error = "--beacon needs the name of a point of the scene, like river.";
                        return false;
                    }

                    beacon = value.Trim();
                    break;
                case "--typed-credit":
                    if (value is not ("full" or "half"))
                    {
                        error = $"--typed-credit needs full or half, got '{value}'.";
                        return false;
                    }

                    result = result with { Credit = value == "full" ? CreditRules.FullTyped : CreditRules.Default };
                    break;
                case "--assume-known":
                    var lemmas = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (lemmas.Length == 0)
                    {
                        error = "--assume-known needs one or more nouns, comma separated, like madera,agua.";
                        return false;
                    }

                    result = result with { AssumeKnown = lemmas };
                    break;
                case "--look":
                    if (!LookControl.TryParseDegrees(value, out var lookYaw, out var lookPitch))
                    {
                        error = $"--look needs a yaw and a pitch in degrees, like 120,-5 (the pitch within 89); got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.Look(lookYaw, lookPitch));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--walk-to":
                    if (!VoxelStep.TryParseColumn(value, out var walkX, out var walkZ))
                    {
                        error = $"--walk-to needs a block column and row, like 60,52; got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.WalkTo(walkX, walkZ));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--interact":
                    if (!VoxelStep.TryParseBlock(value, out var interactX, out var interactY, out var interactZ))
                    {
                        error = $"--interact needs a block, like 9,11,17; got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.Interact(interactX, interactY, interactZ));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--place":
                    var at = value.IndexOf('@', StringComparison.Ordinal);
                    if (at <= 0 || !VoxelStep.TryParseBlock(value[(at + 1)..], out var placeX, out var placeY, out var placeZ))
                    {
                        error = $"--place needs an item and a block, like antorcha@22,12,21; got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.Place(value[..at].Trim(), placeX, placeY, placeZ));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--pour":
                    if (!VoxelStep.TryParseBlock(value, out var pourX, out var pourY, out var pourZ))
                    {
                        error = $"--pour needs a block, like 9,11,17; got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.Pour(pourX, pourY, pourZ));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--select":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot < 1 || slot > 10)
                    {
                        error = $"--select needs a hotbar slot from 1 to 10 (the number keys), got '{value}'.";
                        return false;
                    }

                    steps.Add(VoxelStep.Select(slot));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                case "--craft":
                    if (value.Trim().Length == 0)
                    {
                        error = "--craft needs an item, like antorcha.";
                        return false;
                    }

                    steps.Add(VoxelStep.Craft(value.Trim()));

                    script.Add(new ScriptStep(ScriptStepKind.Move, steps[^1]));
                    break;
                default:
                    break;
            }
        }

        if (screenshot is null && (framesGiven || script.Count > 0 || beacon is not null || journal || inventory || crafting))
        {
            error = "--frames, --look, --walk-to, --teleport, --interact, --place, --pour, --select, --craft, --talk, --say, --speak, --wav, --leave, --lesson, --lesson-run, --book-practise, --lesson-next, --lesson-skip, --lesson-help, --lesson-pick, --shot, --sleep, --report, --report-open, --journal, --inventory, --crafting and --beacon steps need --screenshot (the script).";
            return false;
        }

        if (result.SkipToSession is not null && (result.LoadPath is not null || result.Session is not null || playtest))
        {
            // The skip builds a new game's earlier sessions (V12c): a save has its own, --session only renames the session, and a playtest is one real session.
            error = "--skip-to-session starts a new game as a later play session: it cannot be combined with --load (a save has its own sessions), --session (choose one of the two) or --playtest (a playtest is a learner's own session).";
            return false;
        }

        if (playtest)
        {
            // The playtest profile (S12): the mic check is on unless --no-mic-check; a fresh start has its pre-check, a load its recall check
            // (an explicit --check still wins).
            micCheck = micCheck == MicCheckMode.Auto ? MicCheckMode.Force : micCheck;
            check ??= result.LoadPath is null ? CheckKind.Pre : CheckKind.Recall;
        }

        // A --report-open leaves the panel open, and a paused world takes no further steps: it must be the last step of the script (S21).
        var openAt = script.FindIndex(static step => step.Kind == ScriptStepKind.ReportOpen);
        if (openAt >= 0 && openAt != script.Count - 1)
        {
            error = "--report-open leaves the report panel open, so it must be the last step of the script (use --report to save a report in the middle).";
            return false;
        }

        if (script.Any(static step => step.Kind == ScriptStepKind.Pick) && check is null)
        {
            error = "--pick answers a check item: it needs --check.";
            return false;
        }

        options = result with
        {
            Playtest = playtest,
            StrictSave = strictSave || playtest,
            ScreenshotPath = screenshot,
            Says = says,
            VoxelSteps = steps,
            Script = script,
            Beacon = beacon,
            Journal = journal,
            JournalPhrases = journalPhrases,
            BookStart = bookPage ?? (journalPhrases ? BookPage.Phrases : null),
            BookVerb = bookVerb,
            BookDetailLines = bookDetailLines,
            WindowWidth = windowWidth,
            WindowHeight = windowHeight,
            TextScale = textScale,
            ShowInventory = inventory,
            ShowCrafting = crafting,
            Greyscale = greyscale,
            MicCheck = micCheck,
            Check = check,
        };
        error = string.Empty;
        return true;
    }
}
