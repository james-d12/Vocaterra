using System.Globalization;
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
/// content bundle folder instead of searching for <c>content/game-v1</c>.
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
        + "  --journal               open the journal once the lines are sent (needs --screenshot)\n";

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

    /// <summary>Whether the journal is opened by the scripted run (once the lines are sent, or at the start without <c>--say</c>).</summary>
    public bool Journal { get; init; }

    /// <summary>Whether the run is the scripted screenshot run.</summary>
    public bool IsScreenshotRun => ScreenshotPath is not null;

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

            if (arg == "--talk")
            {
                steps.Add(ScriptStep.Talk());
                continue;
            }

            if (arg is not ("--seed" or "--screenshot" or "--frames" or "--walk-right" or "--say" or "--content" or "--point" or "--point-nearest" or "--skip"))
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
                case "--skip":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 1)
                    {
                        error = $"--skip needs a positive number of minutes, got '{value}'.";
                        return false;
                    }

                    steps.Add(ScriptStep.Skip(minutes));
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

        if (screenshot is null && (framesGiven || walkGiven || steps.Count > 0 || point is not null || pointNearest is not null || journal))
        {
            error = "--frames, --walk-right, --say, --skip, --talk, --point, --point-nearest and --journal need --screenshot.";
            return false;
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
            Point = point,
            PointNearest = pointNearest,
            Journal = journal,
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
