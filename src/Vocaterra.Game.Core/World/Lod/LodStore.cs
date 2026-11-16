using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Lod;

/// <summary>Where the store keeps what it must not forget (B07c): the game gives it files; the store only hands bytes over. Calls come from any thread.</summary>
public interface ILodPersistence
{
    /// <summary>The bytes saved for a section, or <see langword="null"/> when there are none.</summary>
    /// <param name="key">The section.</param>
    byte[]? Load(LodSectionKey key);

    /// <summary>Keeps the bytes of a section (<see cref="LodStore"/>'s own format).</summary>
    /// <param name="key">The section.</param>
    /// <param name="blob">The bytes.</param>
    void Save(LodSectionKey key, byte[] blob);
}

/// <summary>
/// The far view's store of sections (B07c): every level of detail the world has been given, a section a key (level and place) and a packed array, whatever made it (the model, real chunks, a mip of real sections). A section is never replaced by a
/// worse one (a model section never replaces a real one), every change is told on <see cref="Changed"/> for the far view to remake its tiles, and everything is handed to <see cref="ILodPersistence"/> as it is put, so the far world builds up and survives a restart.
/// Safe from any thread.
/// </summary>
public sealed class LodStore
{
    private readonly ConcurrentDictionary<LodSectionKey, LodEntry> _memory = new();
    private readonly ILodPersistence? _disk;

    /// <summary>Creates a store.</summary>
    /// <param name="disk">Where the sections are kept between runs, or <see langword="null"/> for none.</param>
    /// <param name="maxMipLevel">The highest level real sections are mipped up to.</param>
    public LodStore(ILodPersistence? disk = null, int maxMipLevel = 4)
    {
        _disk = disk;
        MaxMipLevel = maxMipLevel;
    }

    /// <summary>The highest level that is made by mipping real sections.</summary>
    public int MaxMipLevel { get; }

    /// <summary>The real sections put since the far view last drained the queue (a model section fills a place nothing was drawn from, so it is not told).</summary>
    public ConcurrentQueue<LodSectionKey> Changed { get; } = new();

    /// <summary>How many sections the store holds in memory.</summary>
    public int Count => _memory.Count;

    /// <summary>The bytes the sections hold in memory.</summary>
    public long Bytes => _memory.Values.Sum(static e => (long)e.Packed.Length);

    /// <summary>The stored section's entry, from memory or else from disk, or <see langword="null"/>.</summary>
    /// <param name="key">The section.</param>
    public LodEntry? Entry(LodSectionKey key)
    {
        if (_memory.TryGetValue(key, out var entry))
        {
            return entry;
        }

        if (_disk?.Load(key) is { Length: > 2 } blob)
        {
            entry = new LodEntry(blob[2..], (LodOrigin)blob[0], blob[1]);
            return _memory.GetOrAdd(key, entry);
        }

        return null;
    }

    /// <summary>The stored section, or <see langword="null"/>.</summary>
    /// <param name="key">The section.</param>
    public LodSection? Get(LodSectionKey key) => Entry(key) is { } entry ? LodSection.Unpack(entry.Packed) : null;

    /// <summary>Keeps a section unless the store holds a better one (a model section never replaces a real one, and none replaces another model section).</summary>
    /// <param name="key">The section.</param>
    /// <param name="section">The section.</param>
    /// <param name="origin">Where it came from.</param>
    /// <param name="realMask">For a level-0 real section, which of its eight chunks are real.</param>
    /// <returns>Whether it was kept.</returns>
    public bool Put(LodSectionKey key, LodSection section, LodOrigin origin, byte realMask = 0)
    {
        ArgumentNullException.ThrowIfNull(section);
        var existing = Entry(key);
        if (existing is not null && (origin == LodOrigin.Model || Rank(existing) > Rank(origin, realMask)))
        {
            return false;
        }

        var entry = new LodEntry(section.Pack(), origin, realMask);
        _memory[key] = entry;
        if (_disk is not null)
        {
            var blob = new byte[entry.Packed.Length + 2];
            blob[0] = (byte)origin;
            blob[1] = realMask;
            entry.Packed.CopyTo(blob, 2);
            _disk.Save(key, blob);
        }

        if (origin != LodOrigin.Model)
        {
            Changed.Enqueue(key); // a model section only fills what no tile was made from; real data changes what a tile shows
        }

        return true;
    }

    private static int Rank(LodEntry entry) => Rank(entry.Origin, entry.RealMask);

    private static int Rank(LodOrigin origin, byte mask) => origin == LodOrigin.Model ? 0 : origin == LodOrigin.Real && mask != 0xFF ? 1 : 2;
}

/// <summary>The sections of one footprint of the far view at a level, bottom to top: a column of sections (<see cref="LodSection"/>s one over another from the world's floor to its ceiling).</summary>
/// <param name="SyMin">The y of the lowest section.</param>
/// <param name="Sections">The sections, from <paramref name="SyMin"/> up.</param>
public sealed record LodStack(int SyMin, LodSection[] Sections);

/// <summary>Where the far view gets the sections of a footprint (B07c).</summary>
public interface ILodSectionSource
{
    /// <summary>The stack of sections of the footprint (<paramref name="sx"/>, <paramref name="sz"/>) at a level, every section from the world's floor to its ceiling.</summary>
    /// <param name="level">The level.</param>
    /// <param name="sx">The footprint's x, in sections of the level.</param>
    /// <param name="sz">The footprint's z.</param>
    LodStack Stack(int level, int sx, int sz);
}

/// <summary>
/// The far world (B07c): the store, with the model to fill what the store does not hold yet. A stack is always whole: if any section of it is missing the whole stack is made from the model and kept (a real section already there is kept in its place),
/// so a tile is never made from half a stack, and real data arrives into the same keys and replaces the model's.
/// </summary>
public sealed class LodWorld : ILodSectionSource
{
    private readonly LodModelSections _model;

    /// <summary>Creates the far world.</summary>
    /// <param name="store">The store.</param>
    /// <param name="model">The model that fills it.</param>
    /// <param name="minY">The world's lowest y.</param>
    /// <param name="maxY">One above the world's highest y.</param>
    public LodWorld(LodStore store, LodModelSections model, int minY, int maxY)
    {
        Store = store;
        _model = model;
        MinY = minY;
        MaxY = maxY;
    }

    /// <summary>The store.</summary>
    public LodStore Store { get; }

    /// <summary>The world's lowest y.</summary>
    public int MinY { get; }

    /// <summary>One above the world's highest y.</summary>
    public int MaxY { get; }

    /// <summary>The first and last section y of a stack at a level: every section that holds a block of the world.</summary>
    /// <param name="level">The level.</param>
    public (int Min, int Max) SyRange(int level) => SyRange(level, MinY, MaxY);

    /// <summary>The first and last section y of a stack at a level, for a world from <paramref name="minY"/> to <paramref name="maxY"/>.</summary>
    /// <param name="level">The level.</param>
    /// <param name="minY">The world's lowest y.</param>
    /// <param name="maxY">One above the world's highest y.</param>
    public static (int Min, int Max) SyRange(int level, int minY, int maxY) => (minY >> (LodSection.Shift + level), (maxY - 1) >> (LodSection.Shift + level));

    /// <inheritdoc />
    public LodStack Stack(int level, int sx, int sz)
    {
        var (min, max) = SyRange(level);
        var sections = new LodSection?[max - min + 1];
        var missing = false;
        for (var i = 0; i < sections.Length; i++)
        {
            sections[i] = Store.Get(new LodSectionKey(level, sx, min + i, sz));
            missing |= sections[i] is null;
        }

        if (missing)
        {
            var made = _model.Stack(level, sx, sz, min, max);
            for (var i = 0; i < sections.Length; i++)
            {
                var key = new LodSectionKey(level, sx, min + i, sz);
                _ = Store.Put(key, made[i], LodOrigin.Model);
                sections[i] ??= Store.Get(key) ?? made[i];
            }
        }

        return new LodStack(min, sections!);
    }

    /// <summary>Makes real a level-0 stack from the generator's chunks (every layer of the four chunk columns it covers).</summary>
    /// <param name="sx">The stack's x, in sections.</param>
    /// <param name="sz">The stack's z.</param>
    /// <param name="chunk">The chunk at (x, y, z) in chunks, or <see langword="null"/> where there is none (air).</param>
    public void IngestStack(int sx, int sz, Func<int, int, int, ChunkData?> chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var (min, max) = SyRange(0);
        var sections = new LodSection[max - min + 1];
        for (var i = 0; i < sections.Length; i++)
        {
            var section = LodSection.Air();
            for (var k = 0; k < 8; k++)
            {
                var (dx, dy, dz) = (k & 1, k >> 2, (k >> 1) & 1);
                if (chunk((2 * sx) + dx, (2 * (min + i)) + dy, (2 * sz) + dz) is { IsEmpty: false } data)
                {
                    CopyChunk(section, data.Blocks, dx, dy, dz);
                }
            }

            section.Simplify();
            sections[i] = section;
        }

        LightStack(sections);
        for (var i = 0; i < sections.Length; i++)
        {
            var key = new LodSectionKey(0, sx, min + i, sz);
            if (Store.Put(key, sections[i], LodOrigin.Real, 0xFF))
            {
                Upgrade(key);
            }
        }
    }

    /// <summary>Takes a real chunk (an edited one, or one a save loaded) into the store: its voxels replace the model's or the older real ones, the stack is lit again, and the levels above that are wholly real are mipped again.</summary>
    /// <param name="coord">The chunk.</param>
    /// <param name="blocks">The chunk's blocks (index = (y * 16 + z) * 16 + x).</param>
    public void IngestChunk(VoxelChunkCoord coord, ReadOnlySpan<BlockId> blocks)
    {
        var (sx, sy, sz) = (coord.X >> 1, coord.Y >> 1, coord.Z >> 1);
        var stack = Stack(0, sx, sz);
        var index = sy - stack.SyMin;
        if ((uint)index >= (uint)stack.Sections.Length)
        {
            return;
        }

        var section = stack.Sections[index].Copy();
        var (dx, dy, dz) = (coord.X & 1, coord.Y & 1, coord.Z & 1);
        CopyChunk(section, blocks, dx, dy, dz);
        section.Simplify();
        stack.Sections[index] = section;
        LightStack(stack.Sections);
        var key = new LodSectionKey(0, sx, sy, sz);
        var existing = Store.Entry(key);
        var mask = (byte)((existing is { Origin: LodOrigin.Real } ? existing.RealMask : 0) | (1 << ((dy << 2) | (dz << 1) | dx)));
        if (Store.Put(key, section, LodOrigin.Real, mask))
        {
            Upgrade(key);
        }
    }

    // The chunk's blocks into the eight-chunk section at the chunk's place in it (plants and the like left out).
    private static void CopyChunk(LodSection section, ReadOnlySpan<BlockId> blocks, int dx, int dy, int dz)
    {
        var (bx, by, bz) = (dx * 16, dy * 16, dz * 16);
        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var block = blocks[(((y << 4) | z) << 4) | x];
                    section.SetBlock(LodSection.Index(bx + x, by + y, bz + z), LodBlocks.IsFar(block) ? block : BlockId.Air);
                }
            }
        }
    }

    // The sky light from the top of the stack down, a section at a time.
    private static void LightStack(LodSection[] sections)
    {
        var sky = new byte[LodSection.Size * LodSection.Size];
        Array.Fill(sky, (byte)15);
        var below = new byte[sky.Length];
        for (var i = sections.Length - 1; i >= 0; i--)
        {
            LodMip.Light(sections[i], sky, below);
            (sky, below) = (below, sky);
        }
    }

    /// <summary>Mips the sections above a real section that are wholly made of real ones, up to the store's highest mip level.</summary>
    /// <param name="key">The real section that changed.</param>
    public void Upgrade(LodSectionKey key)
    {
        var at = key;
        while (at.Level < Store.MaxMipLevel)
        {
            var parent = at.Parent;
            var children = new LodSection[8];
            for (var c = 0; c < 8; c++)
            {
                var entry = Store.Entry(parent.Child(c & 1, c >> 2, (c >> 1) & 1));
                if (entry is not { IsWhollyReal: true })
                {
                    return;
                }

                children[c] = LodSection.Unpack(entry.Packed);
            }

            if (!Store.Put(parent, LodMip.Parent(children), LodOrigin.RealMip))
            {
                return;
            }

            at = parent;
        }
    }
}
