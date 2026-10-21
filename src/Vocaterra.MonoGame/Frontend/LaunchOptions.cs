using System.Globalization;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The command line: <c>--seed &lt;ulong&gt;</c> (default 42), and for the orchestrator's rendering check
/// <c>--screenshot &lt;path.png&gt; [--frames &lt;n&gt;] [--walk-right &lt;ticks&gt;] [--say &lt;line&gt;]...</c>, which runs n frames on a fixed clock, saves the
/// back buffer and exits. <c>--say</c> walks the player to the guide, opens the dialogue panel and sends the line (repeat it for several
/// lines), so nobody has to type. <c>--content &lt;dir&gt;</c> names a content bundle folder instead of searching for <c>content/game-v1</c>.
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
        + "  --say <line>            walk to the guide, open the dialogue, type and send the line; repeat for more (needs --screenshot)\n";

    /// <summary>The world seed.</summary>
    public ulong Seed { get; init; } = DefaultSeed;

    /// <summary>The content bundle folder, or <see langword="null"/> to search for <c>content/game-v1</c>.</summary>
    public string? ContentDirectory { get; init; }

    /// <summary>The lines the scripted run says to the guide, in order (empty for no dialogue).</summary>
    public IReadOnlyList<string> Says { get; init; } = [];

    /// <summary>Where to save the screenshot, or <see langword="null"/> for an interactive run.</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>The frames to run before the screenshot (only used with <see cref="ScreenshotPath"/>).</summary>
    public int Frames { get; init; } = DefaultScreenshotFrames;

    /// <summary>The number of leading ticks during which the scripted input holds 'right' (only used with <see cref="ScreenshotPath"/>).</summary>
    public int WalkRightTicks { get; init; }

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
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                error = string.Empty;
                return false;
            }

            if (arg is not ("--seed" or "--screenshot" or "--frames" or "--walk-right" or "--say" or "--content"))
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

        if (screenshot is null && (framesGiven || walkGiven || says.Count > 0))
        {
            error = "--frames, --walk-right and --say need --screenshot.";
            return false;
        }

        options = result with
        {
            ScreenshotPath = screenshot,
            Says = says,
            Frames = says.Count > 0 && !framesGiven ? DefaultSayFrames : result.Frames,
        };
        error = string.Empty;
        return true;
    }
}
