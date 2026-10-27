namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The key that opened a text box (T or E on an NPC, W for Escribir) is typed by the platform as well, once, in the next frame at the latest: the
/// text event must not take it. A key armed here is swallowed by the first matching letter, and forgotten after two frames.
/// </summary>
public sealed class OpenKeySwallow
{
    private char? _key;
    private int _frames;

    /// <summary>The key that is to be swallowed (lower case), or <see langword="null"/> for none.</summary>
    public void Arm(char? key)
    {
        _key = key;
        _frames = 2;
    }

    /// <summary>Once a frame: the armed key is forgotten when its two frames are over.</summary>
    public void Tick()
    {
        if (_frames > 0 && --_frames == 0)
        {
            _key = null;
        }
    }

    /// <summary>Whether <paramref name="typed"/> is the armed key, which it then takes (the key is swallowed once).</summary>
    public bool Take(char typed)
    {
        if (_key is { } key && char.ToLowerInvariant(typed) == key)
        {
            _key = null;
            return true;
        }

        return false;
    }
}
