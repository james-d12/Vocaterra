using System.Globalization;
using Vocaterra.Game;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The command line: <c>--seed &lt;ulong&gt;</c> (default 42), and for the orchestrator's rendering check
/// <c>--screenshot &lt;path.png&gt; [--frames &lt;n&gt;] [--walk-right &lt;ticks&gt;] [--say &lt;line&gt;]... [--point &lt;col,row&gt; | --point-nearest &lt;lemma&gt;] [--journal]</c>,
/// which runs n frames on a fixed clock, saves the back buffer and exits. <c>--say</c> walks the player to the guide, opens the dialogue panel
/// and sends the line (repeat it for several lines), so nobody has to type. <c>--point</c> puts the mouse cursor on a tile for those lines
/// (<c>--point-nearest</c> on the tile of that noun nearest the player when each line is sent), so <c>es un árbol</c> names something.
/// <c>--journal</c> opens the journal once the lines are sent (at the start without <c>--say</c>). <c>--skip &lt;minutes&gt;</c> moves the stored
/// time on without stepping the world and <c>--talk</c> closes and reopens the dialogue (the guide may then ask a review question); both are
/// steps of the script in the order given with the <c>--say</c> lines (<c>--say A --skip 11 --say B</c>). <c>--content &lt;dir&gt;</c> names a
/// content bundle folder instead of searching for <c>content/game-v1</c>. <c>--wav &lt;file&gt;</c> and <c>--speak &lt;text&gt;</c> are script steps like
/// <c>--say</c> for spoken input without a microphone: the first feeds a WAV file, the second synthesizes the text with Kokoro, and both are
/// recognized by Vosk and evaluated as spoken input. <c>--typed-credit full|half</c> sets the credit a correct typed use earns (default half,
/// D-game-6) and <c>--no-speech</c> skips loading Vosk.
/// <para>
/// <c>--voxel</c> (S2) runs the 3D first-person scene instead of the 2D slice, on the draft route (or the file of <c>--scene</c>) at <c>--seed</c>.
/// Its scripted run is <c>--look yaw,pitch</c>, <c>--walk-to x,z</c>, <c>--interact x,y,z</c>, <c>--place item@x,y,z</c> and <c>--select</c> in the
/// order given, with <c>--beacon &lt;point&gt;</c> standing the beacon column on a named point; <c>--journal</c> and <c>--inventory</c> open those
/// screens at the end. The guide's options do not apply in 3D until S3 and say so.
/// </para>
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>The seed used when <c>--seed</c> is absent.</summary>
    public const ulong DefaultSeed = 42;

    /// <summary>The frame count used with <c>--screenshot</c> when <c>--frames</c> is absent.</summary>
    public const int DefaultScreenshotFrames = 120;

    /// <summary>The frame count used with <c>--screenshot</c> and <c>--say</c> when <c>--frames</c> is absent (walking and typing take longer).</summary>
    public const int DefaultSayFrames = 240;

    /// <summary>The usage text printed on a command-line error or <c>--help</c>.</summary>
    public const string Usage =
        "usage: Vocaterra.MonoGame [--seed <ulong>] [--content <dir>] [--screenshot <path.png> [--frames <n>] [--walk-right <ticks>] [--say <line>]...]\n"
        + "  --seed <ulong>          the world seed (default 42)\n"
        + "  --content <dir>         the content bundle folder (default: content/game-v1 found above the executable or the current directory)\n"
        + "  --screenshot <path>     run on a fixed clock, save the back buffer as a PNG and exit\n"
        + "  --frames <n>            frames to run before the screenshot, one tick each (default 120, or 240 with --say; needs --screenshot)\n"
        + "  --walk-right <ticks>    hold 'right' for the first n ticks (scripted input; needs --screenshot)\n"
        + "  --say <line>            walk to the guide, open the dialogue, type and send the line; repeat for more (needs --screenshot)\n"
        + "  --point <col,row>       put the mouse cursor on that tile for the --say lines (needs --screenshot)\n"
        + "  --point-nearest <lemma> put the cursor on the nearest tile of that noun (arbol, piedra, hierba, tierra) for each --say line (needs --screenshot)\n"
        + "  --skip <minutes>        move the stored time on (no world steps) at this point of the --say lines; order matters (needs --screenshot)\n"
        + "  --talk                  close and reopen the dialogue at this point, so the guide may ask a review question (needs --screenshot)\n"
        + "  --journal               open the journal once the lines are sent (needs --screenshot)\n"
        + "  --walk <col>            walk until the player stands in this column (a world step; needs --screenshot)\n"
        + "  --mine <col,row>        hold the mine command on that tile until it is gone (a world step; needs --screenshot)\n"
        + "  --place <item>@<col,row> select the hotbar slot holding the item and place it on that tile (needs --screenshot)\n"
        + "  --select <slot>         select a hotbar slot, 1 to 10 as the number keys give them (needs --screenshot)\n"
        + "  --inventory             open the inventory screen and keep it open (needs --screenshot)\n"
        + "  --craft <item>          make one item (mesa, puerta, horno...) from the inventory and the stations in reach (needs --screenshot)\n"
        + "  --crafting              open the crafting panel and keep it open (needs --screenshot)\n"
        + "  --wav <file>            say a 16-bit PCM WAV file (16 kHz mono, or any rate: it is converted) as spoken input; a step like --say (needs --screenshot)\n"
        + "  --speak <text>          synthesize the text with Kokoro (cached in ~/.cache/vocaterra/tts), then recognize and evaluate it as spoken input; a step like --say (needs --screenshot)\n"
        + "  --typed-credit <full|half> the credit a correct typed use earns (default half; spoken input always earns full)\n"
        + "  --listen <seconds>      record from the real microphone for 1 to 8 seconds as if the push-to-talk key were held, then send it; a step like --say (needs --screenshot)\n"
        + "  --teleport <col,row>    debug: put the player's feet on that tile (a world step; no walking; needs --screenshot)\n"
        + "  --grant <noun>:<n>      debug: give the player n of a noun's item, e.g. carbón:20 or madera:10 (a step; needs --screenshot)\n"
        + "  --no-speech             do not load the speech models (typing only)\n"
        + "  --status <listening|thinking> show that status line of the dialogue panel once the script is done, for the screenshot (needs --screenshot)\n"
        + "  --voxel                 run the 3D first-person scene (S2, S3) instead of the 2D slice; --say talks to the guide, the 2D-only options (--wav, --speak, --listen, --walk, --mine...) do not apply\n"
        + "  --scene <file>          the scene layout for --voxel (default: game/scene.json in the bundle)\n"
        + "  --look <yaw,pitch>      3D: face this direction in degrees (yaw 0 north, 90 east; pitch positive up); a step (needs --voxel and --screenshot)\n"
        + "  --walk-to <x,z>         3D: walk to this block column with Move commands, within half a block or until the tick limit (a step)\n"
        + "  --interact <x,y,z>      3D: hold the gather command on this block until it is gathered or the tick limit (a step; aims the view at it)\n"
        + "  --place <item>@<x,y,z>  3D: place an item (madera, antorcha, wood...) in this empty cell (a step; with three numbers it is the 3D form)\n"
        + "  --beacon <point>        3D: stand the beacon column on a named point of the scene (camp, firering, workbench, stall, signpost, village, house1, bridge, river...)\n"
        + "Push-to-talk: hold Tab while the dialogue is open, or hold V next to the guide to open it and listen.\n";

    /// <summary>The world seed.</summary>
    public ulong Seed { get; init; } = DefaultSeed;

    /// <summary>The content bundle folder, or <see langword="null"/> to search for <c>content/game-v1</c>.</summary>
    public string? ContentDirectory { get; init; }

    /// <summary>The lines the scripted run says to the guide, in order (empty for no dialogue).</summary>
    public IReadOnlyList<string> Says { get; init; } = [];

    /// <summary>
    /// The scripted run in the order given: the <c>--say</c> lines with the <c>--skip</c> and <c>--talk</c> steps between them (empty for no
    /// dialogue). <see cref="Says"/> holds the lines alone.
    /// </summary>
    public IReadOnlyList<ScriptStep> Steps { get; init; } = [];

    /// <summary>Where to save the screenshot, or <see langword="null"/> for an interactive run.</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>The frames to run before the screenshot (only used with <see cref="ScreenshotPath"/>).</summary>
    public int Frames { get; init; } = DefaultScreenshotFrames;

    /// <summary>The number of leading ticks during which the scripted input holds 'right' (only used with <see cref="ScreenshotPath"/>).</summary>
    public int WalkRightTicks { get; init; }

    /// <summary>The tile the scripted mouse cursor sits on for the <c>--say</c> lines, or <see langword="null"/>.</summary>
    public TilePoint? Point { get; init; }
    /// <summary>The noun whose nearest tile the scripted cursor sits on for each <c>--say</c> line, or <see langword="null"/>.</summary>
    public string? PointNearest { get; init; }

    /// <summary>The credit a correct use earns per kind of input (<c>--typed-credit</c>); typed input earns half by default.</summary>
    public CreditRules Credit { get; init; } = CreditRules.Default;

    /// <summary>The panel status to show once the script is done (<c>--status</c>: <c>listening</c> or <c>thinking</c>), or <see langword="null"/>.</summary>
    public string? Status { get; init; }

    /// <summary>Whether <c>--no-speech</c> was given: Vosk is not loaded.</summary>
    public bool NoSpeech { get; init; }

    /// <summary>
    /// Whether the run loads the speech models: an interactive run does (unless <c>--no-speech</c>), a scripted run only when it has a
    /// <c>--wav</c>, <c>--speak</c> or <c>--listen</c> step, so a typed demo does not pay for Vosk's load.
    /// </summary>
    public bool NeedsSpeech => !NoSpeech && (!IsScreenshotRun || Steps.Any(static s => s.Kind is ScriptStepKind.Wav or ScriptStepKind.Speak or ScriptStepKind.Listen));

    /// <summary>Whether the journal is opened by the scripted run (once the lines are sent, or at the start without <c>--say</c>).</summary>
    public bool Journal { get; init; }

    /// <summary>Whether the scripted run records from the real microphone (it has a <c>--listen</c> step).</summary>
    public bool UsesMicrophone => Steps.Any(static s => s.Kind == ScriptStepKind.Listen) || (!IsScreenshotRun && NeedsSpeech);

    /// <summary>Whether the run is the scripted screenshot run.</summary>
    public bool IsScreenshotRun => ScreenshotPath is not null;

    /// <summary>Whether <c>--voxel</c> was given: the 3D first-person scene runs instead of the 2D slice.</summary>
    public bool Voxel { get; init; }

    /// <summary>The scene layout file named by <c>--scene</c>, or <see langword="null"/> to look for the bundle's scene and then the draft route.</summary>
    public string? SceneFile { get; init; }

    /// <summary>The 3D scripted steps (<c>--look</c>, <c>--walk-to</c>, <c>--interact</c>, <c>--place</c>, <c>--select</c>) in the order given.</summary>
    public IReadOnlyList<VoxelStep> VoxelSteps { get; init; } = [];

    /// <summary>The named point of the scene the beacon starts on (<c>--beacon</c>), or <see langword="null"/>.</summary>
    public string? Beacon { get; init; }

    /// <summary>Whether the 3D scripted run opens the inventory screen and keeps it open (<c>--inventory</c>).</summary>
    public bool ShowInventory { get; init; }

    /// <summary>Parses the arguments. On failure <paramref name="error"/> says why (or is empty for <c>--help</c>) and the options are defaults.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out LaunchOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(args);
        var result = new LaunchOptions();
        options = result;
        string? screenshot = null;
        var framesGiven = false;
        var walkGiven = false;
        var says = new List<string>();
        var steps = new List<ScriptStep>();
        TilePoint? point = null;
        string? pointNearest = null;
        var journal = false;
        string? status = null;
        var voxel = false;
        var voxelSteps = new List<VoxelStep>();
        var voxelOnly = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                error = string.Empty;
                return false;
            }

            if (arg == "--journal")
            {
                journal = true;
                continue;
            }

            if (arg == "--voxel")
            {
                voxel = true;
                continue;
            }

            if (arg == "--talk")
            {
                steps.Add(ScriptStep.Talk());
                continue;
            }

            if (arg == "--inventory")
            {
                steps.Add(ScriptStep.Inventory());
                continue;
            }

            if (arg == "--crafting")
            {
                steps.Add(ScriptStep.CraftingPanel());
                continue;
            }

            if (arg == "--no-speech")
            {
                result = result with { NoSpeech = true };
                continue;
            }

            if (arg is not ("--seed" or "--screenshot" or "--frames" or "--walk-right" or "--say" or "--content" or "--point" or "--point-nearest" or "--skip"
                or "--walk" or "--mine" or "--place" or "--select" or "--craft" or "--wav" or "--speak" or "--typed-credit" or "--status" or "--listen"
                or "--teleport" or "--grant" or "--scene" or "--look" or "--walk-to" or "--interact" or "--beacon"))
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
                case "--say":
                    if (value.Trim().Length == 0)
                    {
                        error = "--say needs a line to say.";
                        return false;
                    }

                    says.Add(value);
                    steps.Add(ScriptStep.Say(value));
                    break;
                case "--wav":
                    if (value.Trim().Length == 0)
                    {
                        error = "--wav needs a WAV file.";
                        return false;
                    }

                    steps.Add(ScriptStep.Wav(value));
                    break;
                case "--speak":
                    if (value.Trim().Length == 0)
                    {
                        error = "--speak needs a line to speak.";
                        return false;
                    }

                    steps.Add(ScriptStep.Speak(value));
                    break;
                case "--listen":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var listenSeconds) || listenSeconds < 1 || listenSeconds > 8)
                    {
                        error = $"--listen needs a number of seconds from 1 to 8, got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Listen(listenSeconds));
                    break;
                case "--status":
                    if (value is not ("listening" or "thinking"))
                    {
                        error = $"--status needs listening or thinking, got '{value}'.";
                        return false;
                    }

                    status = value;
                    break;
                case "--typed-credit":
                    if (value is not ("full" or "half"))
                    {
                        error = $"--typed-credit needs full or half, got '{value}'.";
                        return false;
                    }

                    result = result with { Credit = value == "full" ? CreditRules.FullTyped : CreditRules.Default };
                    break;
                case "--skip":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 1)
                    {
                        error = $"--skip needs a positive number of minutes, got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Skip(minutes));
                    break;
                case "--walk":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var column))
                    {
                        error = $"--walk needs a column, like 517; got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Walk(column));
                    break;
                case "--mine":
                    if (!TryParseTile(value, out var mineTile))
                    {
                        error = $"--mine needs a column and a row, like 517,182; got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Mine(mineTile));
                    break;
                case "--teleport":
                    if (!TryParseTile(value, out var teleportTile))
                    {
                        error = $"--teleport needs a column and a row, like 503,211; got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Teleport(teleportTile));
                    break;
                case "--grant":
                    var colon = value.LastIndexOf(':');
                    if (colon <= 0 || !int.TryParse(value[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var grantCount) || grantCount < 1)
                    {
                        error = $"--grant needs a noun and a count, like carbón:20; got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Grant(value[..colon].Trim(), grantCount));
                    break;
                case "--place":
                    var at = value.IndexOf('@', StringComparison.Ordinal);
                    if (at > 0 && VoxelStep.TryParseBlock(value[(at + 1)..], out var placeX, out var placeY, out var placeZ))
                    {
                        voxelSteps.Add(VoxelStep.Place(value[..at].Trim(), placeX, placeY, placeZ));
                        voxelOnly.Add(arg);
                        break;
                    }

                    if (at <= 0 || !TryParseTile(value[(at + 1)..], out var placeTile))
                    {
                        error = $"--place needs an item and a tile, like madera@517,182 (2D), or a block, like madera@20,11,19 (3D); got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Place(value[..at].Trim(), placeTile));
                    break;
                case "--scene":
                    if (value.Length == 0)
                    {
                        error = "--scene needs a layout file.";
                        return false;
                    }

                    result = result with { SceneFile = value };
                    voxelOnly.Add(arg);
                    break;
                case "--beacon":
                    if (value.Trim().Length == 0)
                    {
                        error = "--beacon needs the name of a point of the scene, like river.";
                        return false;
                    }

                    result = result with { Beacon = value.Trim() };
                    voxelOnly.Add(arg);
                    break;
                case "--look":
                    if (!LookControl.TryParseDegrees(value, out var lookYaw, out var lookPitch))
                    {
                        error = $"--look needs a yaw and a pitch in degrees, like 120,-5 (the pitch within 89); got '{value}'.";
                        return false;
                    }

                    voxelSteps.Add(VoxelStep.Look(lookYaw, lookPitch));
                    voxelOnly.Add(arg);
                    break;
                case "--walk-to":
                    if (!VoxelStep.TryParseColumn(value, out var walkX, out var walkZ))
                    {
                        error = $"--walk-to needs a block column and row, like 60,52; got '{value}'.";
                        return false;
                    }

                    voxelSteps.Add(VoxelStep.WalkTo(walkX, walkZ));
                    voxelOnly.Add(arg);
                    break;
                case "--interact":
                    if (!VoxelStep.TryParseBlock(value, out var interactX, out var interactY, out var interactZ))
                    {
                        error = $"--interact needs a block, like 20,11,19; got '{value}'.";
                        return false;
                    }

                    voxelSteps.Add(VoxelStep.Interact(interactX, interactY, interactZ));
                    voxelOnly.Add(arg);
                    break;
                case "--craft":
                    if (value.Trim().Length == 0)
                    {
                        error = "--craft needs an item, like mesa.";
                        return false;
                    }

                    steps.Add(ScriptStep.Craft(value.Trim()));
                    break;
                case "--select":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot < 1 || slot > 10)
                    {
                        error = $"--select needs a hotbar slot from 1 to 10 (the number keys), got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Select(slot));
                    voxelSteps.Add(VoxelStep.Select(slot));
                    break;
                case "--point":
                    if (!TryParseTile(value, out var tile))
                    {
                        error = $"--point needs a column and a row, like 40,30; got '{value}'.";
                        return false;
                    }

                    point = tile;
                    break;
                case "--point-nearest":
                    if (value.Trim().Length == 0)
                    {
                        error = "--point-nearest needs a noun, like arbol.";
                        return false;
                    }

                    pointNearest = value.Trim();
                    break;
                case "--content":
                    if (value.Length == 0)
                    {
                        error = "--content needs a folder.";
                        return false;
                    }

                    result = result with { ContentDirectory = value };
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
                default:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
                    {
                        error = $"--walk-right needs a non-negative integer, got '{value}'.";
                        return false;
                    }

                    walkGiven = true;
                    result = result with { WalkRightTicks = ticks };
                    break;
            }
        }

        if (screenshot is null && (framesGiven || walkGiven || steps.Count > 0 || voxelSteps.Count > 0 || point is not null || pointNearest is not null || journal || status is not null))
        {
            error = "--frames, --walk-right, --say, --wav, --speak, --skip, --talk, --point, --point-nearest, --journal, --look, --walk-to, --interact and --place need --screenshot.";
            return false;
        }

        if (!voxel && voxelOnly.Count > 0)
        {
            error = $"{string.Join(", ", voxelOnly.Distinct())} work only with --voxel.";
            return false;
        }

        var showInventory = false;
        if (voxel)
        {
            // The 2D world and its tools are not part of the 3D scene (S4 retires them); --say is the guide in 3D (S3). Say so instead of ignoring an option.
            var unsupported = steps.Where(static s => s.Kind is not (ScriptStepKind.Select or ScriptStepKind.Inventory or ScriptStepKind.Say)).Select(static s => s.Kind.ToString()).Distinct().ToList();
            if (unsupported.Count > 0 || walkGiven || point is not null || pointNearest is not null || status is not null)
            {
                error = "With --voxel only --say talks to the guide (S3); --wav, --speak, --listen, --skip, --talk, --walk, --mine, --craft, --crafting, --teleport, --grant, --walk-right, --point, --point-nearest and --status belong to the 2D slice (S4 retires it).";
                return false;
            }

            showInventory = steps.Any(static s => s.Kind == ScriptStepKind.Inventory);
            steps = [];
        }

        if (point is not null && pointNearest is not null)
        {
            error = "Give --point or --point-nearest, not both.";
            return false;
        }

        options = result with
        {
            ScreenshotPath = screenshot,
            Says = says,
            Steps = steps,
            Frames = steps.Count > 0 && !framesGiven ? DefaultSayFrames : result.Frames,
            Voxel = voxel,
            VoxelSteps = voxel ? voxelSteps : [],
            ShowInventory = showInventory,
            Point = point,
            PointNearest = pointNearest,
            Journal = journal,
            Status = status,
        };
        error = string.Empty;
        return true;
    }

    /// <summary>Parses <c>col,row</c> (two non-negative integers) into a tile.</summary>
    private static bool TryParseTile(string value, out TilePoint tile)
    {
        tile = default;
        var parts = value.Split(',');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var column)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var row))
        {
            return false;
        }

        tile = new TilePoint(column, row);
        return true;
    }
}
