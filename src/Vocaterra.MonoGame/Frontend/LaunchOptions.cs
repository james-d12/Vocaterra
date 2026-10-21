using System.Globalization;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The command line: <c>--seed &lt;ulong&gt;</c> (default 42), and for the orchestrator's rendering check
/// <c>--screenshot &lt;path.png&gt; [--frames &lt;n&gt;] [--walk-right &lt;ticks&gt;]</c>, which runs n frames on a fixed clock, saves the back buffer and exits.
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>The seed used when <c>--seed</c> is absent.</summary>
    public const ulong DefaultSeed = 42;

    /// <summary>The frame count used with <c>--screenshot</c> when <c>--frames</c> is absent.</summary>
    public const int DefaultScreenshotFrames = 120;

    /// <summary>The usage text printed on a command-line error or <c>--help</c>.</summary>
    public const string Usage =
        "usage: Vocaterra.MonoGame [--seed <ulong>] [--screenshot <path.png> [--frames <n>] [--walk-right <ticks>]]\n"
        + "  --seed <ulong>          the world seed (default 42)\n"
        + "  --screenshot <path>     run on a fixed clock, save the back buffer as a PNG and exit\n"
        + "  --frames <n>            frames to run before the screenshot, one tick each (default 120; needs --screenshot)\n"
        + "  --walk-right <ticks>    hold 'right' for the first n ticks (scripted input; needs --screenshot)\n";

    /// <summary>The world seed.</summary>
    public ulong Seed { get; init; } = DefaultSeed;

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
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                error = string.Empty;
                return false;
            }

            if (arg is not ("--seed" or "--screenshot" or "--frames" or "--walk-right"))
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

        if (screenshot is null && (framesGiven || walkGiven))
        {
            error = "--frames and --walk-right need --screenshot.";
            return false;
        }

        options = result with { ScreenshotPath = screenshot };
        error = string.Empty;
        return true;
    }
}
