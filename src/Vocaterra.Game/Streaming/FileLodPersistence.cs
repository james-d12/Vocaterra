using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The far view's sections on disk (B07c): one small file for each section (<c>L{level}/{x}/{z}/{y}.lod</c>, already deflated by the store), under a folder for one world that a manifest ties to the generator's version and the world-generation data's hash, so a
/// changed world starts again from nothing. Reads are direct; writes are queued to a thread of their own and made through a temporary file, so a crash never leaves half a section and no frame waits for a disk.
/// </summary>
public sealed class FileLodPersistence : ILodPersistence, IDisposable
{
    private readonly string _root;
    private readonly BlockingCollection<(string Path, byte[] Blob)> _queue = new(new ConcurrentQueue<(string, byte[])>());
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

    private string PathOf(LodSectionKey key) => Path.Combine(_root, "L" + key.Level, key.X.ToString(System.Globalization.CultureInfo.InvariantCulture), key.Z.ToString(System.Globalization.CultureInfo.InvariantCulture), key.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".lod");

    /// <inheritdoc />
    public byte[]? Load(LodSectionKey key)
    {
        try
        {
            var path = PathOf(key);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Save(LodSectionKey key, byte[] blob)
    {
        if (_queue.IsAddingCompleted)
        {
            return;
        }

        _ = Interlocked.Increment(ref _queued);
        _queue.Add((PathOf(key), blob));
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
        foreach (var (path, blob) in _queue.GetConsumingEnumerable())
        {
            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + ".tmp";
                File.WriteAllBytes(temporary, blob);
                File.Move(temporary, path, overwrite: true);
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
        _queue.Dispose();
    }
}
