namespace Vocaterra.Game.Scripted;

/// <summary>A <c>--shot</c> step of a scripted run (the world's script or a check battery's): the picture waits for the next frame that may be taken.</summary>
public sealed class ShotRequest
{
    private string? _path;

    /// <summary>Whether a picture waits for its frame.</summary>
    public bool Pending => _path is not null;

    /// <summary>Asks for a picture of the screen, to <paramref name="path"/>.</summary>
    public void Request(string path) => _path = path;

    /// <summary>Takes the waiting picture's path, or <see langword="null"/> when none waits.</summary>
    public string? Take()
    {
        var path = _path;
        _path = null;
        return path;
    }
}
