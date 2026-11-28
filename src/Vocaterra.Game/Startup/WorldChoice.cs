namespace Vocaterra.Game.Startup;

/// <summary>Which world a run plays in (<c>--world</c>, B07).</summary>
public enum WorldChoice
{
    /// <summary>The tall procedural world streamed in round the valley, with the far view: the game's world.</summary>
    Streamed,

    /// <summary>The bounded valley alone, exactly as the game was before the B series (and what the scripted runs of the valley compare against).</summary>
    Valley,
}

/// <summary>The names of the worlds as <c>--world</c> takes them.</summary>
public static class WorldChoices
{
    /// <summary>The names, for a usage error.</summary>
    public static string Names => string.Join(" or ", Enum.GetNames<WorldChoice>().Select(static name => name.ToLowerInvariant()));

    /// <summary>The run's world for a world's type as its folder keeps it (Q01).</summary>
    /// <param name="kind">The type.</param>
    public static WorldChoice Of(Core.Worlds.WorldKind kind) => kind == Core.Worlds.WorldKind.Valley ? WorldChoice.Valley : WorldChoice.Streamed;

    /// <summary>The type a run's world is kept as in a world's folder (Q01).</summary>
    /// <param name="choice">The run's world.</param>
    public static Core.Worlds.WorldKind KindOf(WorldChoice choice) => choice == WorldChoice.Valley ? Core.Worlds.WorldKind.Valley : Core.Worlds.WorldKind.Streamed;

    /// <summary>The world a name stands for, or <see langword="null"/>.</summary>
    /// <param name="value">The name.</param>
    public static WorldChoice? Parse(string value) => value switch
    {
        "streamed" => WorldChoice.Streamed,
        "valley" => WorldChoice.Valley,
        _ => null,
    };
}
