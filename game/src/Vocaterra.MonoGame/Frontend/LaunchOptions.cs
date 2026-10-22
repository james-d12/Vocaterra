using System.Globalization;
using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The command line of the game (the 3D first-person scene, the prototype's only world since S4): <c>--seed &lt;ulong&gt;</c> (default 42), the
/// content bundle and the scene, and for the scripted run (<c>--screenshot &lt;path.png&gt;</c>) a list of steps in the order given: <c>--look</c>,
/// <c>--walk-to</c>, <c>--interact</c>, <c>--place</c>, <c>--select</c>, <c>--craft</c>, and, since S6, the talk steps <c>--talk &lt;npc&gt;</c> (walk to the
/// NPC, stop about two blocks away facing it, open the panel), <c>--say</c> (type and send one line to the NPC the panel is open on; with no panel
/// open it first talks to the guide, as in S3), <c>--leave</c> (close the panel) and <c>--shot &lt;png&gt;</c> (save the screen now and go on). All of them run in
/// the order given (<see cref="Script"/>). <c>--beacon</c> stands the beacon on a named point, <c>--journal</c>, <c>--inventory</c> and <c>--crafting</c>
/// open those screens at the end. <c>--voxel</c> is accepted and ignored (the 3D world is the only one since S4). Since S7 the scripted run also has
/// <c>--speak &lt;line&gt;</c> (synthesize the line with Kokoro and say it to the NPC as if spoken: the scripted spoken path of the demos and tests) and
/// <c>--wav &lt;file&gt;</c> (a recorded WAV said the same way), and the mic check has <c>--mic-check</c> (force it) and <c>--no-mic-check</c> (skip it).
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>The NPCs <c>--talk</c> accepts: the scene's names for them.</summary>
    public static readonly string[] TalkTargets = ["guide", "shopkeeper", "villager"];

    /// <summary>The seed used when <c>--seed</c> is absent.</summary>
    public const ulong DefaultSeed = 42;

    /// <summary>The frame count used with <c>--screenshot</c> when <c>--frames</c> is absent.</summary>
    public const int DefaultScreenshotFrames = 120;

    /// <summary>The usage text printed on a command-line error or <c>--help</c>.</summary>
    public const string Usage =
        "usage: Vocaterra.MonoGame [--seed <ulong>] [--content <dir>] [--scene <file>] [--screenshot <path.png> [--frames <n>] [--say <line>]...]\n"
        + "  --seed <ulong>          the world seed (default 42)\n"
        + "  --content <dir>         the content bundle folder (default: content/game-v1 found above the executable or the current directory)\n"
        + "  --scene <file>          the scene layout (default: game/scene.json in the bundle)\n"
        + "  --screenshot <path>     run on a fixed clock, run the steps below in order, save the back buffer as a PNG and exit\n"
        + "  --frames <n>            with --screenshot: run at least n ticks before the screenshot (default 120)\n"
        + "  --look <yaw,pitch>      face this direction in degrees (yaw 0 north, 90 east; pitch positive up); a step\n"
        + "  --walk-to <x,z>         walk to this block column with Move commands, within half a block or until the tick limit; a step\n"
        + "  --interact <x,y,z>      hold the gather command on this block until it is gathered or the tick limit (aims at it); a step\n"
        + "  --place <item>@<x,y,z>  place one item (madera, antorcha, wood, torch...) in this empty cell, within reach; a step\n"
        + "  --select <n>            select a hotbar slot, 1 to 10; a step\n"
        + "  --craft <item>          craft the recipe that makes the item (antorcha) with the mesa in reach; a step\n"
        + "  --talk <npc>            walk up to the NPC (guide, shopkeeper, villager), stop about two blocks away facing it and open the dialogue; a step\n"
        + "  --say <line>            type and send the line to the NPC the dialogue is open on (the guide when none is: walk to her and open it) and wait for the reply; a step\n"
        + "  --speak <line>          synthesize the line with Kokoro and say it to the NPC the dialogue is open on as if spoken (Vosk hears it); a step\n"
        + "  --wav <file>            say a recorded WAV file to the NPC the dialogue is open on (16-bit PCM, any rate); a step\n"
        + "  --leave                 close the dialogue (walking on or talking to another NPC closes it too); a step\n"
        + "  --shot <path.png>       save the screen at this point of the script and go on (the final --screenshot is saved when the script is done); a step\n"
        + "  --beacon <point>        stand the beacon column on a named point of the scene (camp, firering, workbench, stall, signpost, village, house1, bridge, river...)\n"
        + "  --journal               open the journal once the steps are done\n"
        + "  --inventory             open the inventory screen once the steps are done\n"
        + "  --crafting              open the crafting panel once the steps are done\n"
        + "  --mic-check             show the mic check at the start (an interactive run shows it by default; a scripted run with --screenshot never does unless asked,\n"
        + "                          and then hears a synthesized \"hola\" when the machine has no microphone)\n"
        + "  --no-mic-check          never show the mic check\n"
        + "  --typed-credit <full|half> the credit a correct typed use earns (default half; spoken input always earns full)\n"
        + "  --no-speech             do not load the speech models (typing only)\n"
        + "  --mute                  the NPCs' pre-rendered voices are off (the panel still shows every line)\n"
        + "  --greyscale             accepted; the world is drawn in full colour (D-game-6 as amended), the grey A/B is not in the 3D world yet\n"
        + "  --voxel                 accepted and ignored (the 3D world is the only world)\n"
        + "Controls: WASD or the arrows walk, Space jumps, the mouse looks; hold the left button to gather, the right to place; E opens the inventory (or\n"
        + "  the crafting panel at the workbench, or talks to the guide when the crosshair is on her); C the crafting panel; J the journal; 1..0 the hotbar.\n"
        + "Push-to-talk: hold V or Space while the dialogue is open, or hold V with the crosshair on an NPC to open it and listen. In the dialogue: R repeat,\n"
        + "  S slower, H help, N \"no sé\", W or Tab type (Tab or Esc back to the buttons), 1 to 3 pick an offered choice, Esc cancels a turn that is thinking, then closes.\n";

    /// <summary>The world seed.</summary>
    public ulong Seed { get; init; } = DefaultSeed;

    /// <summary>The content bundle folder, or <see langword="null"/> to search for <c>content/game-v1</c>.</summary>
    public string? ContentDirectory { get; init; }

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

    /// <summary>The credit a correct use earns per kind of input (<c>--typed-credit</c>); typed input earns half by default.</summary>
    public CreditRules Credit { get; init; } = CreditRules.Default;

    /// <summary>Whether <c>--no-speech</c> was given: Vosk is not loaded.</summary>
    public bool NoSpeech { get; init; }

    /// <summary>Whether the NPCs' voices are off (<c>--mute</c>): the panel shows the lines and nothing plays (S9).</summary>
    public bool Muted { get; init; }

    /// <summary>Whether the journal is opened once the script is done (<c>--journal</c>).</summary>
    public bool Journal { get; init; }

    /// <summary>Whether the inventory screen is opened once the script is done (<c>--inventory</c>).</summary>
    public bool ShowInventory { get; init; }

    /// <summary>Whether the crafting panel is opened once the script is done (<c>--crafting</c>).</summary>
    public bool ShowCrafting { get; init; }

    /// <summary>The named point of the scene the beacon starts on (<c>--beacon</c>), or <see langword="null"/>.</summary>
    public string? Beacon { get; init; }

    /// <summary>Whether <c>--greyscale</c> was given (accepted; the 3D world has no grey copy yet).</summary>
    public bool Greyscale { get; init; }

    /// <summary>Whether the mic check is forced (<c>--mic-check</c>), skipped (<c>--no-mic-check</c>) or left to the default.</summary>
    public MicCheckMode MicCheck { get; init; } = MicCheckMode.Auto;

    /// <summary>Whether the run is the scripted screenshot run.</summary>
    public bool IsScreenshotRun => ScreenshotPath is not null;

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
        var journal = false;
        var inventory = false;
        var crafting = false;
        var greyscale = false;
        var micCheck = MicCheckMode.Auto;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                error = string.Empty;
                return false;
            }

            if (arg is "--journal" or "--inventory" or "--crafting" or "--no-speech" or "--mute" or "--voxel" or "--greyscale" or "--leave" or "--mic-check" or "--no-mic-check")
            {
                switch (arg)
                {
                    case "--leave":
                        script.Add(new ScriptStep(ScriptStepKind.Leave));
                        break;
                    case "--mute":
                        result = result with { Muted = true };
                        break;
                    case "--mic-check":
                        micCheck = MicCheckMode.Force;
                        break;
                    case "--no-mic-check":
                        micCheck = MicCheckMode.Skip;
                        break;
                    case "--journal":
                        journal = true;
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
                    default:
                        break; // --voxel: accepted for old scripts, the 3D world is the only one
                }

                continue;
            }

            if (arg is not ("--seed" or "--screenshot" or "--frames" or "--say" or "--content" or "--scene" or "--look" or "--walk-to" or "--interact"
                or "--place" or "--select" or "--craft" or "--beacon" or "--typed-credit" or "--talk" or "--shot" or "--speak" or "--wav"))
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

                    result = result with { Seed = seed };
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
                case "--talk":
                    if (Array.IndexOf(TalkTargets, value.Trim()) < 0)
                    {
                        error = $"--talk needs an NPC ({string.Join(", ", TalkTargets)}), got '{value}'.";
                        return false;
                    }

                    script.Add(new ScriptStep(ScriptStepKind.Talk, Value: value.Trim()));
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
                case "--scene":
                    if (value.Length == 0)
                    {
                        error = "--scene needs a layout file.";
                        return false;
                    }

                    result = result with { SceneFile = value };
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
            error = "--frames, --look, --walk-to, --interact, --place, --select, --craft, --talk, --say, --speak, --wav, --leave, --shot, --journal, --inventory, --crafting and --beacon steps need --screenshot (the script).";
            return false;
        }

        options = result with
        {
            ScreenshotPath = screenshot,
            Says = says,
            VoxelSteps = steps,
            Script = script,
            Beacon = beacon,
            Journal = journal,
            ShowInventory = inventory,
            ShowCrafting = crafting,
            Greyscale = greyscale,
            MicCheck = micCheck,
        };
        error = string.Empty;
        return true;
    }
}

/// <summary>The kinds of step of the scripted run: the body's steps and the talk steps.</summary>
public enum ScriptStepKind
{
    /// <summary>A step of the body (<see cref="ScriptStep.Step"/>): look, walk, interact, place, select, craft.</summary>
    Move,

    /// <summary>Walk up to the NPC named in <see cref="ScriptStep.Value"/>, stop at the stand-off distance facing it and open the dialogue.</summary>
    Talk,

    /// <summary>Type and send the line in <see cref="ScriptStep.Value"/> to the NPC the dialogue is open on.</summary>
    Say,

    /// <summary>Synthesize the line in <see cref="ScriptStep.Value"/> with Kokoro and say it to the NPC the dialogue is open on as if spoken (S7).</summary>
    Speak,

    /// <summary>Say the WAV file in <see cref="ScriptStep.Value"/> to the NPC the dialogue is open on as if spoken (S7).</summary>
    Wav,

    /// <summary>Close the dialogue.</summary>
    Leave,

    /// <summary>Save the screen to the file in <see cref="ScriptStep.Value"/> and go on.</summary>
    Shot,
}

/// <summary>One step of the scripted run, in the order given on the command line.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Step">For <see cref="ScriptStepKind.Move"/>: the body's step.</param>
/// <param name="Value">For the talk steps: the NPC, the line or the file path.</param>
public sealed record ScriptStep(ScriptStepKind Kind, VoxelStep? Step = null, string Value = "");

/// <summary>Whether the mic check is shown at the start.</summary>
public enum MicCheckMode
{
    /// <summary>The default: shown at the start of an interactive run that loads speech, never in a scripted run.</summary>
    Auto,

    /// <summary><c>--mic-check</c>: shown whatever the run.</summary>
    Force,

    /// <summary><c>--no-mic-check</c>: never shown.</summary>
    Skip,
}
