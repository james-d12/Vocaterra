namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>The containers a move can name by id (I02): the bag, the equipment and the trash, and any container added later (a chest, W24).</summary>
public sealed class SlotContainers
{
    private readonly List<ISlotContainer> _all = [];

    /// <summary>Every container, in the order added.</summary>
    public IReadOnlyList<ISlotContainer> All => _all;

    /// <summary>Adds a container.</summary>
    /// <param name="container">The container; its id must be new and not <see cref="ContainerIds.Held"/>.</param>
    /// <exception cref="ArgumentException">The id is taken, or is the held stack's.</exception>
    public void Add(ISlotContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (container.Id == ContainerIds.Held || Find(container.Id) is not null)
        {
            throw new ArgumentException($"Container id {container.Id} is taken.", nameof(container));
        }

        _all.Add(container);
    }

    /// <summary>The container with <paramref name="id"/>, or <see langword="null"/>.</summary>
    /// <param name="id">The id.</param>
    public ISlotContainer? Find(int id)
    {
        foreach (var container in _all)
        {
            if (container.Id == id)
            {
                return container;
            }
        }

        return null;
    }
}
