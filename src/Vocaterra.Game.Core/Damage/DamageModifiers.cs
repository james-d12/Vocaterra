namespace Vocaterra.Game.Core.Damage;

/// <summary>The damage modifiers the code has, by id (I05a): what <c>game/damage.json</c> may name. A new modifier is a class and one registration; no file that reads damage changes.</summary>
public sealed class DamageModifiers
{
    private readonly Dictionary<string, IDamageModifier> _byId = new(StringComparer.Ordinal);

    /// <summary>The ids registered, in order.</summary>
    public IEnumerable<string> Ids => _byId.Keys;

    /// <summary>The set the game uses: armour.</summary>
    public static DamageModifiers Standard()
    {
        var set = new DamageModifiers();
        set.Add(new ArmourDamageModifier());
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
