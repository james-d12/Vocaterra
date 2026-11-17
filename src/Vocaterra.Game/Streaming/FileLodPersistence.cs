using System.Collections.Concurrent;
using System.Diagnostics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The far view's sections on disk (B07c): a folder for one world, tied by a manifest to the generator's version and the world-generation data's hash (so a changed world starts again from nothing), holding for each level and each 8 x 8 footprints of
/// sections one region file (<c>L{level}/r.{x}.{z}.lodr</c>): a header of 4,096 slots (a section's offset and length) and the sections' packed bytes after it, appended as they are put. A world's far view is hundreds of thousands of sections, so one file each
/// would fill a disk's inodes; a region file is a few hundred. Reads are direct; writes are queued to a thread of their own, so no frame waits for a disk, and a slot is written after its bytes, so a crash of the process leaves the old section or the new, never half (the files are synced to the disk on a timer, <see cref="SyncIntervalMilliseconds"/> after the first write that is not synced yet, and at shutdown, and the manifest when it is written: B07d, the review's P4, where the writer synced up to 48 files whenever its queue ran dry, which a ring of small writes does every few milliseconds). A section saved is answered by <see cref="Load"/> at once, from a table of the writes the writer has not made yet, so the store may drop it from memory as soon as it is saved. Region files only grew; <see cref="Compact"/> rewrites those that hold much dead data (sections written again) keeping every live section. A torn or foreign file reads as missing sections, and a failure on the writer thread costs the section and not the writer. Beside the regions the folder holds the edit journal (<c>edits.txt</c>, B07f): the chunks whose player-made blocks are in the sections, written through the same queue, so a run that loads another save knows which to drop. Shutdown is bounded: the writer drains what is queued, syncs and closes its own files, and the caller waits for it only up to a limit.
/// </summary>
public sealed class FileLodPersistence : ILodPersistence, ILodEditJournal, IDisposable
{
    private const int Slots = 8 * 8 * 64;
    private const int HeaderBytes = 8 + (Slots * 8);
    private const int YOrigin = 16;
    private const int MaxBlobBytes = 1 << 20;

    private readonly string _root;
    private readonly BlockingCollection<(LodSectionKey Key, byte[] Blob, Action? Run)> _queue = new(new ConcurrentQueue<(LodSectionKey, byte[], Action?)>());
    private readonly HashSet<VoxelChunkCoord> _edited = [];
    private readonly string _journalFile;
    private readonly Dictionary<string, FileStream> _writing = [];
    private readonly Dictionary<string, byte[]> _headers = [];
    private readonly Thread _writer;
    private readonly ConcurrentDictionary<LodSectionKey, byte[]> _unwritten = new();
    private readonly ReaderWriterLockSlim _swap = new();
    private readonly long _syncTicks;
    private long _syncs;
    private long _loads;
    private long _compactedFiles;
    private long _reclaimedBytes;
    private long _queued;
    private long _written;
    private volatile bool _writerDone;
    private volatile bool _closed;
    private volatile bool _queueDisposed;

    /// <summary>How long after a write that is not synced yet the writer syncs the files, in milliseconds (an fsync is slow: a ring of small writes made one every few milliseconds before).</summary>
    public const int SyncIntervalMilliseconds = 2_000;

    /// <summary>How long <see cref="Dispose"/> waits for the writer to drain the queue and close its files, in milliseconds.</summary>
    public const int ShutdownLimitMilliseconds = 8_000;

    /// <summary>Opens (or starts) the folder of a world.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="version">What the sections in it are valid for: when it differs from the manifest the folder is cleared.</param>
    /// <param name="syncIntervalMilliseconds">How long after an unsynced write the files are synced (tests give a short one).</param>
    public FileLodPersistence(string root, string version, int syncIntervalMilliseconds = SyncIntervalMilliseconds)
    {
        _syncTicks = (long)(Math.Max(1, syncIntervalMilliseconds) * (Stopwatch.Frequency / 1000.0));
        _root = root;
        _ = Directory.CreateDirectory(root);
        var manifest = Path.Combine(root, "manifest.txt");
        _journalFile = Path.Combine(root, "edits.txt");
        if (!File.Exists(manifest) || File.ReadAllText(manifest) != version)
        {
            File.Delete(_journalFile);
            foreach (var directory in Directory.GetDirectories(root, "L*"))
            {
                Directory.Delete(directory, recursive: true);
            }

            using var stream = new FileStream(manifest, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(System.Text.Encoding.UTF8.GetBytes(version));
            stream.Flush(flushToDisk: true);
        }

        ReadJournal();
        _writer = new Thread(Write) { IsBackground = true, Name = "lod-writer" };
        _writer.Start();
    }

    /// <summary>The folder.</summary>
    public string Root => _root;

    /// <summary>The folder a world's sections go in: <c>VOCATERRA_LOD_DIR</c> or the user's data folder, then the world's own name.</summary>
    /// <param name="world">The world's name (its seed, generator version and data hash).</param>
    public static string FolderFor(string world)
    {
        var baseFolder = Environment.GetEnvironmentVariable("VOCATERRA_LOD_DIR");
        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "vocaterra", "lod");
        }

        return Path.Combine(baseFolder, world);
    }

    /// <summary>The region file of a section's place.</summary>
    /// <param name="key">The section.</param>
    public string RegionFile(LodSectionKey key) => Path.Combine(_root, "L" + key.Level, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"r.{key.X >> 3}.{key.Z >> 3}.lodr"));

    // The slot of a section in its region's header; a section outside the vertical range the header holds is not kept.
    private static int SlotOf(LodSectionKey key) => (uint)(key.Y + YOrigin) < 64 ? (((((key.X & 7) << 3) | (key.Z & 7))) << 6) | (key.Y + YOrigin) : -1;

    // The slots of a region's header, read from the disk once and kept (the writer keeps them current): a miss costs no disk read after the first probe of its region.
    private byte[] HeaderOf(string path)
    {
        lock (_headers)
        {
            if (_headers.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var header = new byte[Slots * 8];
            try
            {
                if (File.Exists(path))
                {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (file.Length >= HeaderBytes)
                    {
                        file.Position = 8;
                        file.ReadExactly(header);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Array.Clear(header);
            }

            if (_headers.Count >= 512)
            {
                _headers.Clear();
            }

            _headers[path] = header;
            return header;
        }
    }

    /// <summary>How many loads the disk answered (a read of a region file; a section still waiting for the writer is answered from memory and is not counted).</summary>
    public long Loads => Interlocked.Read(ref _loads);

    /// <summary>How many times the writer synced the files to the disk.</summary>
    public long Syncs => Interlocked.Read(ref _syncs);

    /// <summary>How many region files have been compacted.</summary>
    public long CompactedFiles => Interlocked.Read(ref _compactedFiles);

    /// <summary>How many bytes compaction took out of the region files.</summary>
    public long ReclaimedBytes => Interlocked.Read(ref _reclaimedBytes);

    /// <summary>How many sections have been saved and not written yet.</summary>
    public int Unwritten => _unwritten.Count;

    /// <inheritdoc />
    public byte[]? Load(LodSectionKey key)
    {
        if (_unwritten.TryGetValue(key, out var waiting))
        {
            return waiting.Length == 0 ? null : waiting; // an empty blob is a drop: it reads as missing
        }

        var slot = SlotOf(key);
        if (slot < 0)
        {
            return null;
        }

        _swap.EnterReadLock(); // a compaction moves a region file's sections and its header together: a load sees the file before or after it, never half
        try
        {
            var path = RegionFile(key);
            var header = HeaderOf(path);
            int offset;
            int length;
            lock (_headers)
            {
                (offset, length) = (BitConverter.ToInt32(header, slot * 8), BitConverter.ToInt32(header, (slot * 8) + 4));
            }

            if (length <= 0 || length > MaxBlobBytes || offset < HeaderBytes)
            {
                return null;
            }

            _ = Interlocked.Increment(ref _loads);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (offset + (long)length > file.Length)
            {
                return null; // torn: the slot points past the end of the file
            }

            var blob = new byte[length];
            file.Position = offset;
            file.ReadExactly(blob);
            return blob;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            _swap.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void Save(LodSectionKey key, byte[] blob)
    {
        if (_closed || SlotOf(key) < 0 || blob.Length > MaxBlobBytes)
        {
            return;
        }

        try
        {
            _unwritten[key] = blob; // answered by Load from now on, until the writer has written these bytes (or newer ones)
            _ = Interlocked.Increment(ref _queued);
            _queue.Add((key, blob, null));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            _ = _unwritten.TryRemove(new KeyValuePair<LodSectionKey, byte[]>(key, blob));
            _ = Interlocked.Increment(ref _written); // closed meanwhile (the world is being left): the section is made again next time
        }
    }

    /// <summary>Waits until everything queued has been written, at most <see cref="ShutdownLimitMilliseconds"/>.</summary>
    public void Flush() => _ = Flush(TimeSpan.FromMilliseconds(ShutdownLimitMilliseconds));

    /// <summary>Waits until everything queued has been written, or the writer has stopped, or the limit has passed.</summary>
    /// <param name="limit">The longest to wait.</param>
    /// <returns>Whether everything queued was written.</returns>
    public bool Flush(TimeSpan limit)
    {
        var until = Environment.TickCount64 + (long)limit.TotalMilliseconds;
        while (Interlocked.Read(ref _written) < Interlocked.Read(ref _queued))
        {
            if (!_writer.IsAlive || Environment.TickCount64 > until)
            {
                return Interlocked.Read(ref _written) >= Interlocked.Read(ref _queued);
            }

            Thread.Sleep(2);
        }

        return true;
    }

    /// <summary>Whether the writer thread has ended (it closes its files on the way out).</summary>
    public bool IsWriterStopped => _writerDone;

    // The journal ------------------------------------------------------------------------------------------------------------------------------

    private void ReadJournal()
    {
        try
        {
            if (!File.Exists(_journalFile))
            {
                return;
            }

            foreach (var line in File.ReadLines(_journalFile))
            {
                var parts = line.Split(' ');
                if (parts.Length == 3 && int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var x) && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var y) && int.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var z))
                {
                    _ = _edited.Add(new VoxelChunkCoord(x, y, z));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _edited.Clear(); // an unreadable journal says nothing about which sections hold edits, so the sections start again
            foreach (var directory in Directory.GetDirectories(_root, "L*"))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string Line(VoxelChunkCoord coord) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{coord.X} {coord.Y} {coord.Z}");

    /// <inheritdoc />
    public IReadOnlyCollection<VoxelChunkCoord> Edited()
    {
        lock (_edited)
        {
            return [.. _edited];
        }
    }

    /// <inheritdoc />
    public void Record(VoxelChunkCoord coord)
    {
        lock (_edited)
        {
            if (!_edited.Add(coord))
            {
                return;
            }
        }

        Enqueue(() => File.AppendAllText(_journalFile, Line(coord) + "\n"));
    }

    /// <inheritdoc />
    public void Replace(IReadOnlyCollection<VoxelChunkCoord> coords)
    {
        ArgumentNullException.ThrowIfNull(coords);
        var text = string.Concat(coords.OrderBy(static c => c.Y).ThenBy(static c => c.Z).ThenBy(static c => c.X).Select(static c => Line(c) + "\n"));
        lock (_edited)
        {
            _edited.Clear();
            _edited.UnionWith(coords);
        }

        Enqueue(() =>
        {
            var temporary = _journalFile + ".tmp";
            File.WriteAllText(temporary, text);
            File.Move(temporary, _journalFile, overwrite: true);
        });
    }

    // A journal write goes through the section writer's queue, so it is made in order with the sections' saves.
    private void Enqueue(Action run)
    {
        if (_closed)
        {
            return;
        }

        try
        {
            _ = Interlocked.Increment(ref _queued);
            _queue.Add((default, [], run));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            _ = Interlocked.Increment(ref _written);
        }
    }

    private void Write()
    {
        var unsynced = false;
        var since = 0L;
        try
        {
            while (true)
            {
                (LodSectionKey Key, byte[] Blob, Action? Run) item;
                if (unsynced)
                {
                    // Wake in time for the sync even when nothing more is queued: the files are synced a fixed time after the first write they have not been synced for, not whenever the queue runs dry.
                    var left = _syncTicks - (Stopwatch.GetTimestamp() - since);
                    if (left <= 0)
                    {
                        SyncAll();
                        unsynced = false;
                        continue;
                    }

                    if (!_queue.TryTake(out item, (int)Math.Min(int.MaxValue, Math.Max(1, left * 1000 / Stopwatch.Frequency))))
                    {
                        if (_queue.IsCompleted)
                        {
                            break;
                        }

                        continue;
                    }
                }
                else if (!_queue.TryTake(out item, Timeout.Infinite))
                {
                    break; // the queue is complete and empty
                }

                try
                {
                    if (item.Run is not null)
                    {
                        item.Run();
                    }
                    else
                    {
                        WriteOne(item.Key, item.Blob);
                        if (!unsynced)
                        {
                            unsynced = true;
                            since = Stopwatch.GetTimestamp();
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // a section that cannot be kept (a full disk, a locked file, a bug) is made again next time; the writer goes on
                    CloseAll();
                }
                finally
                {
                    if (item.Run is null)
                    {
                        _ = _unwritten.TryRemove(new KeyValuePair<LodSectionKey, byte[]>(item.Key, item.Blob)); // only if no newer bytes of the key were saved meanwhile
                    }
                }

                _ = Interlocked.Increment(ref _written);
            }

            SyncAll();
        }
        finally
        {
            CloseAll(); // the writer opened the files and the writer closes them, after its last write
            _writerDone = true;
        }
    }

    private void WriteOne(LodSectionKey key, byte[] blob)
    {
        var path = RegionFile(key);
        if (!_writing.TryGetValue(path, out var file))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (_writing.Count >= 48)
            {
                CloseAll();
            }

            file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (file.Length < HeaderBytes)
            {
                file.SetLength(HeaderBytes);
                file.Position = 0;
                file.Write("LODR"u8);
                file.Write(BitConverter.GetBytes(1));
            }

            _writing[path] = file;
        }

        var slot = SlotOf(key);
        var offset = (int)Math.Max(HeaderBytes, file.Length);
        file.Position = offset;
        file.Write(blob); // the move of the position below puts these bytes to the file before the slot is written: a crash leaves the old section or the new
        file.Position = 8 + (slot * 8L);
        file.Write(BitConverter.GetBytes(offset));
        file.Write(BitConverter.GetBytes(blob.Length));
        file.Flush();
        var header = HeaderOf(path);
        lock (_headers)
        {
            BitConverter.GetBytes(offset).CopyTo(header, slot * 8);
            BitConverter.GetBytes(blob.Length).CopyTo(header, (slot * 8) + 4);
        }
    }

    private void SyncAll()
    {
        if (_writing.Count > 0)
        {
            _ = Interlocked.Increment(ref _syncs);
        }

        foreach (var open in _writing.Values)
        {
            try
            {
                open.Flush(flushToDisk: true);
            }
            catch (IOException)
            {
                // the next write will tell
            }
        }
    }

    private void CloseAll()
    {
        foreach (var open in _writing.Values)
        {
            try
            {
                open.Dispose();
            }
            catch (IOException)
            {
                // closed anyway
            }
        }

        _writing.Clear();
    }

    // Compaction ---------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Rewrites the region files that hold much dead data (a section written again leaves its old bytes behind, and the files only ever grew), keeping every live section: for each file whose dead bytes are at least <paramref name="minDeadBytes"/> and
    /// <paramref name="minDeadFraction"/> of its sections' bytes, a new file is written with the live sections one after the other and put in its place. It runs on the writer thread, one file a queued job, so sections go on being written between files; a load
    /// that comes while a file is being replaced waits for the swap. Asks and returns: <see cref="CompactedFiles"/> and <see cref="ReclaimedBytes"/> say what was done.
    /// </summary>
    /// <param name="minDeadFraction">The share of a file's section bytes that must be dead for it to be rewritten.</param>
    /// <param name="minDeadBytes">The least dead bytes worth a rewrite.</param>
    public void Compact(double minDeadFraction = 0.25, long minDeadBytes = 256 * 1024) =>
        Enqueue(() =>
        {
            foreach (var path in Directory.EnumerateFiles(_root, "r.*.lodr", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList())
            {
                var captured = path;
                Enqueue(() => CompactFile(captured, minDeadFraction, minDeadBytes));
            }
        });

    private void CompactFile(string path, double minDeadFraction, long minDeadBytes)
    {
        var temporary = path + ".tmp";
        try
        {
            using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = read.Length;
            if (length < HeaderBytes)
            {
                return;
            }

            var header = new byte[HeaderBytes];
            read.ReadExactly(header);
            if (!header.AsSpan(0, 4).SequenceEqual("LODR"u8))
            {
                return;
            }

            var live = new List<(int Slot, int Offset, int Length)>();
            long liveBytes = 0;
            for (var slot = 0; slot < Slots; slot++)
            {
                var (offset, size) = (BitConverter.ToInt32(header, 8 + (slot * 8)), BitConverter.ToInt32(header, 12 + (slot * 8)));
                if (size > 0 && size <= MaxBlobBytes && offset >= HeaderBytes && offset + (long)size <= length)
                {
                    live.Add((slot, offset, size));
                    liveBytes += size;
                }
            }

            var dead = length - HeaderBytes - liveBytes;
            if (dead < minDeadBytes || dead < minDeadFraction * (length - HeaderBytes))
            {
                return;
            }

            if (_writing.Remove(path, out var open))
            {
                try
                {
                    open.Dispose(); // the writer's own handle to the file that is about to be replaced
                }
                catch (IOException)
                {
                    // closed anyway
                }
            }

            var table = new byte[Slots * 8];
            long position = HeaderBytes;
            using (var write = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write.SetLength(HeaderBytes);
                write.Position = 0;
                write.Write(header.AsSpan(0, 8));
                write.Position = HeaderBytes;
                var buffer = new byte[MaxBlobBytes];
                foreach (var (slot, offset, size) in live.OrderBy(static l => l.Offset))
                {
                    read.Position = offset;
                    read.ReadExactly(buffer.AsSpan(0, size));
                    write.Write(buffer.AsSpan(0, size));
                    BitConverter.GetBytes((int)position).CopyTo(table, slot * 8);
                    BitConverter.GetBytes(size).CopyTo(table, (slot * 8) + 4);
                    position += size;
                }

                write.Position = 8;
                write.Write(table);
                write.Flush(flushToDisk: true);
            }

            _swap.EnterWriteLock();
            try
            {
                File.Move(temporary, path, overwrite: true);
                lock (_headers)
                {
                    _headers[path] = table;
                }
            }
            finally
            {
                _swap.ExitWriteLock();
            }

            _ = Interlocked.Increment(ref _compactedFiles);
            _ = Interlocked.Add(ref _reclaimedBytes, length - position);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            // the old file stays as it was: nothing was moved over it
            try
            {
                File.Delete(temporary);
            }
            catch (Exception inner) when (inner is IOException or UnauthorizedAccessException)
            {
                // left for the next run to overwrite
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _ = Shutdown(TimeSpan.FromMilliseconds(ShutdownLimitMilliseconds));

    /// <summary>
    /// Stops taking writes, lets the writer drain the queue, sync and close its files, and waits for it up to <paramref name="limit"/>. A writer that is not done by then (a slow disk) is left to finish on its own thread, a background thread that never holds the
    /// process: nothing here touches its files, so there is no second closer to race it.
    /// </summary>
    /// <param name="limit">The longest to wait.</param>
    /// <returns>Whether the writer finished in time.</returns>
    public bool Shutdown(TimeSpan limit)
    {
        if (_queueDisposed)
        {
            return true;
        }

        _closed = true;
        _queue.CompleteAdding();
        var finished = _writer.Join(limit);
        if (finished)
        {
            _queueDisposed = true;
            _queue.Dispose();
        }

        return finished;
    }
}
