namespace Vocaterra.Game.Core.Damage;

/// <summary>The damage modifiers the code has, by id (I05a): what <c>game/damage.json</c> may name. A new modifier is a class and one registration; no file that reads damage changes.</summary>
public sealed class DamageModifiers
{
    private readonly Dictionary<string, IDamageModifier> _byId = new(StringComparer.Ordinal);

    /// <summary>The ids registered, in order.</summary>
    public IEnumerable<string> Ids => _byId.Keys;

    /// <summary>The set the game uses: armour, and the wear a hit puts on what is worn (I07).</summary>
    /// <param name="wear">The wear engine the <c>wear</c> modifier wears items through, or <see langword="null"/> for a set with no wear (a test's, the validator's).</param>
    public static DamageModifiers Standard(Func<Items.Wearing.ItemWear>? wear = null)
    {
        var set = new DamageModifiers();
        set.Add(new ArmourDamageModifier());
        set.Add(new WearDamageModifier(wear));
        return set;
    }

    /// <summary>Registers a modifier.</summary>
    /// <param name="modifier">The modifier.</param>
    /// <exception cref="ArgumentException">The id is already registered.</exception>
    public void Add(IDamageModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        if (!_byId.TryAdd(modifier.Id, modifier))
        {
            throw new ArgumentException($"The damage modifier '{modifier.Id}' is registered twice.", nameof(modifier));
        }
    }

    /// <summary>The modifier with an id, or <see langword="null"/> when none has it.</summary>
    /// <param name="id">The id.</param>
    public IDamageModifier? Find(string id) => _byId.GetValueOrDefault(id);
}
