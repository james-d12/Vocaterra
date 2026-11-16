using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The far view's sections on disk (B07c): a folder for one world, tied by a manifest to the generator's version and the world-generation data's hash (so a changed world starts again from nothing), holding for each level and each 8 x 8 footprints of
/// sections one region file (<c>L{level}/r.{x}.{z}.lodr</c>): a header of 4,096 slots (a section's offset and length) and the sections' packed bytes after it, appended as they are put. A world's far view is hundreds of thousands of sections, so one file each
/// would fill a disk's inodes; a region file is a few hundred. Reads are direct; writes are queued to a thread of their own, so no frame waits for a disk, and a slot is written after its bytes, so a crash leaves the old section or the new, never half.
/// </summary>
public sealed class FileLodPersistence : ILodPersistence, IDisposable
{
    private const int Slots = 8 * 8 * 64;
    private const int HeaderBytes = 8 + (Slots * 8);
    private const int YOrigin = 16;

    private readonly string _root;
    private readonly BlockingCollection<(LodSectionKey Key, byte[] Blob)> _queue = new(new ConcurrentQueue<(LodSectionKey, byte[])>());
    private readonly Dictionary<string, FileStream> _writing = [];
    private readonly Thread _writer;
    private long _queued;
    private long _written;

    /// <summary>Opens (or starts) the folder of a world.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="version">What the sections in it are valid for: when it differs from the manifest the folder is cleared.</param>
    public FileLodPersistence(string root, string version)
    {
        _root = root;
        _ = Directory.CreateDirectory(root);
        var manifest = Path.Combine(root, "manifest.txt");
        if (!File.Exists(manifest) || File.ReadAllText(manifest) != version)
        {
            foreach (var directory in Directory.GetDirectories(root, "L*"))
            {
                Directory.Delete(directory, recursive: true);
            }

            File.WriteAllText(manifest, version);
        }

        _writer = new Thread(Write) { IsBackground = true, Name = "far-view sections" };
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

    /// <inheritdoc />
    public byte[]? Load(LodSectionKey key)
    {
        var slot = SlotOf(key);
        if (slot < 0)
        {
            return null;
        }

        try
        {
            var path = RegionFile(key);
            if (!File.Exists(path))
            {
                return null;
            }

            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> entry = stackalloc byte[8];
            file.Position = 8 + (slot * 8L);
            file.ReadExactly(entry);
            var (offset, length) = (BitConverter.ToInt32(entry[..4]), BitConverter.ToInt32(entry[4..]));
            if (length <= 0)
            {
                return null;
            }

            var blob = new byte[length];
            file.Position = offset;
            file.ReadExactly(blob);
            return blob;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Save(LodSectionKey key, byte[] blob)
    {
        if (_queue.IsAddingCompleted || SlotOf(key) < 0)
        {
            return;
        }

        _ = Interlocked.Increment(ref _queued);
        _queue.Add((key, blob));
    }

    /// <summary>Waits until everything queued has been written.</summary>
    public void Flush()
    {
        while (Interlocked.Read(ref _written) < Interlocked.Read(ref _queued) && _writer.IsAlive)
        {
            Thread.Sleep(2);
        }
    }

    private void Write()
    {
        foreach (var (key, blob) in _queue.GetConsumingEnumerable())
        {
            try
            {
                var path = RegionFile(key);
                if (!_writing.TryGetValue(path, out var file))
                {
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    if (_writing.Count >= 48)
                    {
                        foreach (var open in _writing.Values)
                        {
                            open.Dispose();
                        }

                        _writing.Clear();
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

                var offset = (int)Math.Max(HeaderBytes, file.Length);
                file.Position = offset;
                file.Write(blob);
                file.Flush();
                file.Position = 8 + (SlotOf(key) * 8L);
                file.Write(BitConverter.GetBytes(offset));
                file.Write(BitConverter.GetBytes(blob.Length));
                file.Flush();
            }
            catch (IOException)
            {
                // a section that cannot be kept is made again next time
            }

            _ = Interlocked.Increment(ref _written);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _queue.CompleteAdding();
        _writer.Join();
        foreach (var open in _writing.Values)
        {
            open.Dispose();
        }

        _queue.Dispose();
    }
}
