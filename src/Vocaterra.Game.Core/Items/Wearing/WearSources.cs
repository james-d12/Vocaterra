namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>The wear sources the code has, by id (I07): what an item's <see cref="DurabilitySpec.Source"/> may name. A new source is a class and one registration.</summary>
public sealed class WearSources
{
    private readonly Dictionary<string, IWearSource> _byId = new(StringComparer.Ordinal);

    /// <summary>The ids registered, in order.</summary>
    public IEnumerable<string> Ids => _byId.Keys;

    /// <summary>The set the game uses: use, time and damage.</summary>
    public static WearSources Standard()
    {
        var set = new WearSources();
        set.Add(new UseWearSource());
        set.Add(new TimeWearSource());
        set.Add(new DamageWearSource());
        return set;
    }

    /// <summary>Registers a source.</summary>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentException">The id is already registered.</exception>
    public void Add(IWearSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!_byId.TryAdd(source.Id, source))
        {
            throw new ArgumentException($"The wear source '{source.Id}' is registered twice.", nameof(source));
        }
    }

    /// <summary>The source with an id, or <see langword="null"/> when none has it.</summary>
    /// <param name="id">The id.</param>
    public IWearSource? Find(string id) => _byId.GetValueOrDefault(id);
}
