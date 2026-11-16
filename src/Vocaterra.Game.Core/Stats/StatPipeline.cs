namespace Vocaterra.Game.Core.Stats;

/// <summary>
/// The player's stat contributors, in the order they were registered (I03a). <see cref="Build"/> asks each in turn and returns the sheet; the simulation builds it every tick and when
/// the equipment changes. Registering a contributor is the whole change a new source of stats needs.
/// </summary>
public sealed class StatPipeline
{
    private readonly List<IStatContributor> _contributors = [];

    /// <summary>The registered contributors, in order.</summary>
    public IReadOnlyList<IStatContributor> Contributors => _contributors;

    /// <summary>Registers a contributor after the others.</summary>
    /// <param name="contributor">The source.</param>
    public void Add(IStatContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        _contributors.Add(contributor);
    }

    /// <summary>Builds the sheet of every contributor now.</summary>
    public StatSheet Build()
    {
        var builder = new StatBuilder();
        foreach (var contributor in _contributors)
        {
            contributor.Contribute(builder);
        }

        return builder.Build();
    }
}
