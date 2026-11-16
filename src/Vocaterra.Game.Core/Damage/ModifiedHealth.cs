using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Damage;

/// <summary>
/// The damage pipeline (I05a): an <see cref="IPlayerHealth"/> that runs the modifiers <c>game/damage.json</c> names for the cause, in order, over the damage before the inner health takes it. A cause the file
/// does not list, or lists with no modifier, passes unchanged. Damage reduced to nothing is not passed on.
/// </summary>
public sealed class ModifiedHealth : IPlayerHealth
{
    private readonly IPlayerHealth _inner;
    private readonly DamageData _data;
    private readonly DamageModifiers _modifiers;
    private readonly Func<StatSheet> _stats;

    /// <summary>Decorates a health.</summary>
    /// <param name="inner">The health that takes the modified damage.</param>
    /// <param name="data">Which modifiers apply to which cause.</param>
    /// <param name="modifiers">The modifiers by id.</param>
    /// <param name="stats">The player's current stat sheet, read at each hit.</param>
    public ModifiedHealth(IPlayerHealth inner, DamageData data, DamageModifiers modifiers, Func<StatSheet> stats)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    /// <inheritdoc />
    public bool IsDead => _inner.IsDead;

    /// <inheritdoc />
    public void Damage(int amount, DamageCause cause)
    {
        var ids = _data.ModifiersFor(cause.Id);
        if (ids.Count > 0)
        {
            var sheet = _stats();
            foreach (var id in ids)
            {
                if (_modifiers.Find(id) is { } modifier)
                {
                    amount = modifier.Modify(amount, cause, sheet);
                }
            }
        }

        if (amount > 0)
        {
            _inner.Damage(amount, cause);
        }
    }
}
