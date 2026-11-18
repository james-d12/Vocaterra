namespace Vocaterra.Game.Streaming;

/// <summary>
/// How many worker threads a run uses (B07d, D-game-34 item 42): in play the player's Worker threads setting (Auto, or 1 to <see cref="JobPool.MaxWorkers"/>), where Auto is the processor count less four (the game loop, the sound, the speech and the
/// operating system keep their own), at least one and at most eight; a scripted run, the tests and <c>--prefetch</c> keep the small count their results were made for. The world never depends on the count.
/// </summary>
public static class WorkerPlan
{
    /// <summary>The processors the game leaves to everything else when the count is Auto.</summary>
    public const int ReservedProcessors = 4;

    /// <summary>What Auto means on a machine: the processor count less <see cref="ReservedProcessors"/>, at least one, at most <see cref="JobPool.MaxWorkers"/>.</summary>
    /// <param name="processors">The machine's processor count.</param>
    public static int Auto(int processors) => Math.Clamp(processors - ReservedProcessors, 1, JobPool.MaxWorkers);

    /// <summary>The worker count of a setting's value (0 is Auto; 1 to eight are themselves, and anything else is held to that range).</summary>
    /// <param name="setting">The setting's value: 0 for Auto, else a count.</param>
    /// <param name="processors">The machine's processor count.</param>
    public static int OfSetting(int setting, int processors) => setting <= 0 ? Auto(processors) : Math.Clamp(setting, 1, JobPool.MaxWorkers);

    /// <summary>
    /// The count a run starts with. <c>--prefetch</c> wins (it is the old option: 0 to 2, 0 keeping everything on the game loop's thread); then <c>--workers</c> (the same as the setting, for the run); a scripted run and an agent's
    /// run otherwise keep <see cref="JobPool.ScriptedWorkers"/>; a played run takes the setting.
    /// </summary>
    /// <param name="prefetch"><c>--prefetch</c>, or <see langword="null"/>.</param>
    /// <param name="workersOption"><c>--workers</c> (0 for auto), or <see langword="null"/>.</param>
    /// <param name="scripted">Whether the run is a scripted screenshot run.</param>
    /// <param name="setting">The Worker threads setting's value (0 for Auto).</param>
    /// <param name="processors">The machine's processor count.</param>
    public static int For(int? prefetch, int? workersOption, bool scripted, int setting, int processors)
    {
        if (prefetch is { } fixedCount)
        {
            return Math.Clamp(fixedCount, 0, JobPool.ScriptedWorkers);
        }

        if (workersOption is { } option)
        {
            return OfSetting(option, processors);
        }

        return scripted ? JobPool.ScriptedWorkers : OfSetting(setting, processors);
    }

    /// <summary>The count the settings page changes the running pool to: the setting's own, unless the run is held to a count (a scripted run or <c>--prefetch</c>), which keeps it. A <c>--workers</c> count is only the setting's value for the run, and the page may change it.</summary>
    /// <param name="prefetch"><c>--prefetch</c>, or <see langword="null"/>.</param>
    /// <param name="scripted">Whether the run is scripted.</param>
    /// <param name="setting">The setting's value now.</param>
    /// <param name="processors">The machine's processor count.</param>
    /// <returns>The count to resize to, or <see langword="null"/> for a run whose count is fixed.</returns>
    public static int? Change(int? prefetch, bool scripted, int setting, int processors) =>
        prefetch is not null || scripted ? null : OfSetting(setting, processors);
}
