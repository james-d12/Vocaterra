using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Lod;

/// <summary>Where the store keeps what it must not forget (B07c): the game gives it files; the store only hands bytes over. Calls come from any thread; saves of one key arrive in the order they were made.</summary>
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
/// The chunks whose player-made blocks the far store holds (B07f): the store keeps its sections per world, but the player's edits belong to a save, so a run that loads a save (or starts a new game) on the same world must find out which of the edits it
/// still has in its sections are not the save's and drop them. The journal is the list: a chunk is recorded before its edit is taken into the store (a crash leaves a chunk in the list with no edit in the store, which only costs a section made again), and a
/// run starts by replacing the list with what its own world holds. Calls come from any thread; they reach the disk in the order they were made.
/// </summary>
public interface ILodEditJournal
{
    /// <summary>The chunks that are recorded now (a snapshot).</summary>
    IReadOnlyCollection<VoxelChunkCoord> Edited();

    /// <summary>Records that the store holds, or is about to hold, an edit of a chunk.</summary>
    /// <param name="coord">The chunk.</param>
    void Record(VoxelChunkCoord coord);

    /// <summary>Makes the list exactly these chunks.</summary>
    /// <param name="coords">The chunks.</param>
    void Replace(IReadOnlyCollection<VoxelChunkCoord> coords);
}

/// <summary>
/// The far view's store of sections (B07c): every level of detail the world has been given, a section a key (level and place) and a packed array, whatever made it (the model, real chunks, a mip of real sections). A section is never replaced by a
/// worse one (a model section never replaces a real one; the check, the swap and the hand-over to disk are one step under a lock of the key's stripe, so no two threads can race it and saves of a key reach the disk in the order they were made), every real
/// change is told on <see cref="Changed"/> for the far view to remake its tiles, and real sections go to <see cref="ILodPersistence"/> as they are put, so the far world builds up and survives a restart. Model sections are not kept on disk (the
/// model can make them again) and are dropped from memory, least recently used first, past <see cref="MaxModelBytes"/>. A torn or corrupt section reads as missing. Safe from any thread.
/// </summary>
public sealed class LodStore
{
    private const int Stripes = 64;

    private readonly ConcurrentDictionary<LodSectionKey, Slot> _memory = new();
    private readonly object[] _locks = [.. Enumerable.Range(0, Stripes).Select(static _ => new object())];
    private readonly ILodPersistence? _disk;
    private long _clock;
    private long _modelBytes;

    /// <summary>Creates a store.</summary>
    /// <param name="disk">Where the real sections are kept between runs, or <see langword="null"/> for none.</param>
    /// <param name="maxMipLevel">The highest level real sections are mipped up to.</param>
    /// <param name="maxModelBytes">How many packed bytes of model sections stay in memory.</param>
    public LodStore(ILodPersistence? disk = null, int maxMipLevel = 4, long maxModelBytes = 192L * 1024 * 1024)
    {
        _disk = disk;
        MaxMipLevel = maxMipLevel;
        MaxModelBytes = maxModelBytes;
    }

    /// <summary>The highest level that is made by mipping real sections.</summary>
    public int MaxMipLevel { get; }

    /// <summary>How many packed bytes of model sections stay in memory before the least recently used are dropped.</summary>
    public long MaxModelBytes { get; }

    /// <summary>The real sections put since the far view last drained the queue (a model section fills a place nothing was drawn from, so it is not told).</summary>
    public ConcurrentQueue<LodSectionKey> Changed { get; } = new();

    /// <summary>How many sections the store holds in memory.</summary>
    public int Count => _memory.Count;

    /// <summary>The bytes the sections hold in memory.</summary>
    public long Bytes => _memory.Values.Sum(static s => (long)s.Entry.Packed.Length);

    private sealed class Slot(LodEntry entry, long used)
    {
        public LodEntry Entry { get; } = entry;

        public long Used { get; set; } = used;
    }

    private object StripeOf(LodSectionKey key) => _locks[(int)((uint)HashCode.Combine(key.Level, key.X, key.Y, key.Z) % Stripes)];

    /// <summary>The stored section's entry, from memory or else from disk, or <see langword="null"/> (a section the disk holds torn reads as missing).</summary>
    /// <param name="key">The section.</param>
    public LodEntry? Entry(LodSectionKey key)
    {
        if (_memory.TryGetValue(key, out var slot))
        {
            slot.Used = Interlocked.Increment(ref _clock);
            return slot.Entry;
        }

        if (_disk?.Load(key) is { Length: > 4 } blob && blob[0] is (byte)LodOrigin.Real or (byte)LodOrigin.RealMip)
        {
            var entry = new LodEntry(blob[3..], (LodOrigin)blob[0], blob[1], blob[2]);
            try
            {
                _ = LodSection.Unpack(entry.Packed);
            }
            catch (InvalidDataException)
            {
                return null;
            }

            return _memory.GetOrAdd(key, new Slot(entry, Interlocked.Increment(ref _clock))).Entry;
        }

        return null;
    }

    /// <summary>The stored section, or <see langword="null"/> (also when its bytes are torn: it is dropped, so it is made again).</summary>
    /// <param name="key">The section.</param>
    public LodSection? Get(LodSectionKey key)
    {
        if (Entry(key) is not { } entry)
        {
            return null;
        }

        try
        {
            return LodSection.Unpack(entry.Packed);
        }
        catch (InvalidDataException)
        {
            lock (StripeOf(key))
            {
                // Under the key's lock, so the count of model bytes is exact: only the slot that held these bytes goes.
                if (_memory.TryGetValue(key, out var slot) && ReferenceEquals(slot.Entry, entry))
                {
                    Remove(key, slot);
                }
            }

            return null;
        }
    }

    /// <summary>Keeps a section unless the store holds a better one (a model section never replaces a real one, and none replaces another model section).</summary>
    /// <param name="key">The section.</param>
    /// <param name="section">The section.</param>
    /// <param name="origin">Where it came from.</param>
    /// <param name="realMask">For a level-0 real section, which of its eight chunks are real.</param>
    /// <param name="editedMask">Which of its chunks the player's edits made.</param>
    /// <returns>Whether it was kept.</returns>
    public bool Put(LodSectionKey key, LodSection section, LodOrigin origin, byte realMask = 0, byte editedMask = 0)
    {
        ArgumentNullException.ThrowIfNull(section);
        return PutEntry(key, new LodEntry(section.Pack(), origin, realMask, editedMask));
    }

    // Put, from an entry that is already packed.
    internal bool PutEntry(LodSectionKey key, LodEntry entry)
    {
        lock (StripeOf(key))
        {
            var existing = Entry(key);
            if (existing is not null && (entry.Origin == LodOrigin.Model || Rank(existing) > Rank(entry)))
            {
                return false;
            }

            Keep(key, entry);
        }

        Told(key, entry.Origin);
        return true;
    }

    /// <summary>
    /// Changes what the store holds for a key in one step (under the key's lock, so nothing else is put between the read and the write): <paramref name="change"/> is given the entry that is there (or <see langword="null"/>) and answers the section, origin
    /// and masks to keep, or <see langword="null"/> to leave it. The rank rule does not apply: this is how an edit replaces what the generator made.
    /// </summary>
    /// <param name="key">The section.</param>
    /// <param name="change">The change.</param>
    /// <returns>Whether something was kept.</returns>
    public bool Update(LodSectionKey key, Func<LodEntry?, (LodSection Section, LodOrigin Origin, byte RealMask, byte EditedMask)?> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        LodOrigin origin;
        lock (StripeOf(key))
        {
            var existing = Entry(key);
            if (change(existing) is not { } made)
            {
                return false;
            }

            Keep(key, new LodEntry(made.Section.Pack(), made.Origin, made.RealMask, made.EditedMask));
            origin = made.Origin;
        }

        Told(key, origin);
        return true;
    }

    // Swaps the entry in and hands a real one to the disk; the key's lock is held. The model bytes are counted from the slot that is in memory under the lock (nothing else changes a key's slot without it), never from an entry read earlier.
    private void Keep(LodSectionKey key, LodEntry entry)
    {
        if (_memory.TryGetValue(key, out var old) && old.Entry.Origin == LodOrigin.Model)
        {
            _ = Interlocked.Add(ref _modelBytes, -old.Entry.Packed.Length);
        }

        _memory[key] = new Slot(entry, Interlocked.Increment(ref _clock));
        if (entry.Origin == LodOrigin.Model)
        {
            _ = Interlocked.Add(ref _modelBytes, entry.Packed.Length);
        }
        else if (_disk is not null)
        {
            var blob = new byte[entry.Packed.Length + 3];
            blob[0] = (byte)entry.Origin;
            blob[1] = entry.RealMask;
            blob[2] = entry.EditedMask;
            entry.Packed.CopyTo(blob, 3);
            _disk.Save(key, blob);
        }
    }

    // Takes a slot out of memory and out of the count; the key's lock is held and the slot is the one in memory.
    private void Remove(LodSectionKey key, Slot slot)
    {
        if (_memory.TryRemove(new KeyValuePair<LodSectionKey, Slot>(key, slot)) && slot.Entry.Origin == LodOrigin.Model)
        {
            _ = Interlocked.Add(ref _modelBytes, -slot.Entry.Packed.Length);
        }
    }

    /// <summary>
    /// Forgets a section, in memory and on disk (the next ask makes it again: from the model, or from the generator's chunks). The far view is told, as for any change. A save that is loaded drops what the last run's edits made this way. A disk that is asked about the
    /// key before the drop reached it can still answer the old bytes, so the caller flushes the disk once it has dropped what it means to.
    /// </summary>
    /// <param name="key">The section.</param>
    /// <returns>Whether there was something to drop.</returns>
    public bool Drop(LodSectionKey key)
    {
        lock (StripeOf(key))
        {
            if (Entry(key) is not { } existing)
            {
                return false;
            }

            if (_memory.TryGetValue(key, out var slot))
            {
                Remove(key, slot);
            }

            if (existing.Origin != LodOrigin.Model)
            {
                _disk?.Save(key, []); // an empty blob reads as missing
            }
        }

        Changed.Enqueue(key);
        return true;
    }

    /// <summary>The model sections' packed bytes as the store counts them (the limit <see cref="MaxModelBytes"/> is held against this).</summary>
    public long ModelBytes => Interlocked.Read(ref _modelBytes);

    /// <summary>The model sections' packed bytes counted from the sections themselves, for a test of <see cref="ModelBytes"/>.</summary>
    internal long ModelBytesRecounted() => _memory.Values.Where(static s => s.Entry.Origin == LodOrigin.Model).Sum(static s => (long)s.Entry.Packed.Length);

    private void Told(LodSectionKey key, LodOrigin origin)
    {
        if (origin != LodOrigin.Model)
        {
            Changed.Enqueue(key); // a model section only fills what no tile was made from; real data changes what a tile shows
        }
        else if (Interlocked.Read(ref _modelBytes) > MaxModelBytes)
        {
            Trim();
        }
    }

    // Drops the model sections used least recently, down to three quarters of the limit (they are made again from the model when a tile asks).
    private void Trim()
    {
        var victims = _memory.Where(static p => p.Value.Entry.Origin == LodOrigin.Model).OrderBy(static p => p.Value.Used).ToList();
        foreach (var (key, slot) in victims)
        {
            if (Interlocked.Read(ref _modelBytes) <= MaxModelBytes * 3 / 4)
            {
                break;
            }

            lock (StripeOf(key))
            {
                Remove(key, slot); // only if this slot is still the one in memory; a section put over it meanwhile is not dropped and not counted twice
            }
        }
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
/// so a tile is never made from half a stack, and real data arrives into the same keys and replaces the model's. The player's edits outrank the generator's chunks: a section keeps the chunks an edit made when the generator's stack is ingested over it.
/// </summary>
public sealed class LodWorld : ILodSectionSource
{
    private const int FootprintLocks = 64;

    private readonly LodModelSections _model;
    private readonly object[] _footprints = [.. Enumerable.Range(0, FootprintLocks).Select(static _ => new object())];

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

    // One owner at a time for the level-0 sections of a footprint (B07f): the generator's stack and an edit's chunk both read the stack, light it again and write it back, and neither may write over what the other put in between (the second would take the first's sections for
    // the model's and replace real blocks with older ones).
    private object FootprintOf(int sx, int sz) => _footprints[(int)((uint)HashCode.Combine(sx, sz) % FootprintLocks)];

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

    /// <summary>Makes real a level-0 stack from the generator's chunks (every layer of the four chunk columns it covers); the chunks the player's edits made are kept as they are.</summary>
    /// <param name="sx">The stack's x, in sections.</param>
    /// <param name="sz">The stack's z.</param>
    /// <param name="chunk">The chunk at (x, y, z) in chunks, or <see langword="null"/> where there is none (air).</param>
    public void IngestStack(int sx, int sz, Func<int, int, int, ChunkData?> chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        lock (FootprintOf(sx, sz))
        {
            IngestStackLocked(sx, sz, chunk);
        }
    }

    private void IngestStackLocked(int sx, int sz, Func<int, int, int, ChunkData?> chunk)
    {
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
            var made = sections[i];
            if (Store.Update(key, existing => (KeepEdits(made, existing), LodOrigin.Real, (byte)0xFF, existing?.EditedMask ?? (byte)0)))
            {
                Upgrade(key);
            }
        }
    }

    // The generator's section with the chunks the player's edits made put back over it.
    private static LodSection KeepEdits(LodSection made, LodEntry? existing)
    {
        if (existing is not { EditedMask: not 0 } || existing.Origin == LodOrigin.Model)
        {
            return made;
        }

        LodSection edited;
        try
        {
            edited = LodSection.Unpack(existing.Packed);
        }
        catch (InvalidDataException)
        {
            return made;
        }

        for (var k = 0; k < 8; k++)
        {
            if ((existing.EditedMask & (1 << k)) != 0)
            {
                CopyRegion(made, edited, (k & 1) * 16, (k >> 2) * 16, ((k >> 1) & 1) * 16);
            }
        }

        made.Simplify();
        return made;
    }

    // One chunk's 16 x 16 x 16 voxels, blocks and light, from one section to another.
    private static void CopyRegion(LodSection to, LodSection from, int bx, int by, int bz)
    {
        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var i = LodSection.Index(bx + x, by + y, bz + z);
                    to.Set(i, from.BlockAt(i), from.LightAt(i));
                }
            }
        }
    }

    /// <summary>Takes a real chunk (an edited one, or one a save loaded) into the store: its voxels replace the model's or the generator's, the stack is lit again and every section whose light changed is kept, and the levels above that are mipped again.</summary>
    /// <param name="coord">The chunk.</param>
    /// <param name="blocks">The chunk's blocks (index = (y * 16 + z) * 16 + x).</param>
    public void IngestChunk(VoxelChunkCoord coord, ReadOnlySpan<BlockId> blocks)
    {
        var (sx, sy, sz) = (coord.X >> 1, coord.Y >> 1, coord.Z >> 1);
        lock (FootprintOf(sx, sz))
        {
            IngestChunkLocked(coord, blocks, sx, sy, sz);
        }
    }

    private void IngestChunkLocked(VoxelChunkCoord coord, ReadOnlySpan<BlockId> blocks, int sx, int sy, int sz)
    {
        var stack = Stack(0, sx, sz);
        var index = sy - stack.SyMin;
        if ((uint)index >= (uint)stack.Sections.Length)
        {
            return;
        }

        var (dx, dy, dz) = (coord.X & 1, coord.Y & 1, coord.Z & 1);
        var bit = (byte)(1 << ((dy << 2) | (dz << 1) | dx));
        var key = new LodSectionKey(0, sx, sy, sz);
        var kept = default(LodSection);
        var fromBlocks = blocks.ToArray();
        _ = Store.Update(key, existing =>
        {
            var section = existing is null ? stack.Sections[index].Copy() : LodSection.Unpack(existing.Packed);
            CopyChunk(section, fromBlocks, dx, dy, dz);
            section.Simplify();
            kept = section;
            return (section, LodOrigin.Real, (byte)((existing is { Origin: LodOrigin.Real } ? existing.RealMask : 0) | bit), (byte)((existing?.EditedMask ?? 0) | bit));
        });
        if (kept is null)
        {
            return;
        }

        // The light of the sections under it may change: every one that did is kept as what it was.
        var sections = new LodSection[stack.Sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            sections[i] = i == index ? kept : stack.Sections[i].Copy();
        }

        LightStack(sections);
        for (var i = 0; i <= index; i++)
        {
            var under = sections[i];
            var packed = under.Pack();
            var below = new LodSectionKey(0, sx, stack.SyMin + i, sz);
            _ = Store.Update(below, existing =>
            {
                if (existing is not null && existing.Packed.AsSpan().SequenceEqual(packed))
                {
                    return null;
                }

                return (under, existing?.Origin ?? LodOrigin.Model, existing?.RealMask ?? (byte)0, existing?.EditedMask ?? (byte)0);
            });
        }

        Upgrade(key, refresh: true);
    }

    /// <summary>
    /// Forgets everything the store holds over a footprint (B07f): its level-0 sections and the sections above that were mipped from them, in memory and on disk, so the generator's chunks make them again (and the model stands in meanwhile). A run that loads a save drops
    /// this way the footprints that still hold edits the save does not have.
    /// </summary>
    /// <param name="sx">The footprint's x, in sections.</param>
    /// <param name="sz">The footprint's z.</param>
    /// <returns>How many sections were dropped.</returns>
    public int ResetFootprint(int sx, int sz)
    {
        var dropped = 0;
        lock (FootprintOf(sx, sz))
        {
            for (var level = 0; level <= Store.MaxMipLevel; level++)
            {
                var (min, max) = SyRange(level);
                for (var sy = min; sy <= max; sy++)
                {
                    if (Store.Drop(new LodSectionKey(level, sx >> level, sy, sz >> level)))
                    {
                        dropped++;
                    }
                }
            }
        }

        return dropped;
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

    /// <summary>
    /// Mips the sections above a real section that are wholly made of real ones, up to the store's highest mip level. A child outside the level's vertical range (over the world's ceiling, under its floor) counts as a real section of air or
    /// of stone, so the levels at the top and the bottom of the world are mipped like the rest. With <paramref name="refresh"/> the parents that were mipped before are made again from whatever their children hold now (after an edit).
    /// </summary>
    /// <param name="key">The real section that changed.</param>
    /// <param name="refresh">Whether to remake the parents that exist, from their children as they are.</param>
    public void Upgrade(LodSectionKey key, bool refresh = false)
    {
        var at = key;
        while (at.Level < Store.MaxMipLevel)
        {
            var parent = at.Parent;
            if (refresh && Store.Entry(parent) is not { Origin: LodOrigin.RealMip })
            {
                return;
            }

            var (min, max) = SyRange(at.Level);
            var children = new LodSection[8];
            for (var c = 0; c < 8; c++)
            {
                var child = parent.Child(c & 1, c >> 2, (c >> 1) & 1);
                if (child.Y > max)
                {
                    children[c] = LodSection.Air();
                }
                else if (child.Y < min)
                {
                    children[c] = new LodSection(BlockId.Stone, 0);
                }
                else if (Store.Entry(child) is { } entry && (refresh || entry.IsWhollyReal) && Store.Get(child) is { } section)
                {
                    children[c] = section;
                }
                else
                {
                    return;
                }
            }

            if (!Store.Put(parent, LodMip.Parent(children), LodOrigin.RealMip) && !refresh)
            {
                return;
            }

            at = parent;
        }
    }
}
