namespace Vocaterra.Game.Core.World.Layout;

/// <summary>A layout file is malformed or cannot be built (a bad site name, a prop on uneven ground, a point outside the grid).</summary>
public sealed class SceneLayoutException : FormatException
{
    /// <summary>Creates the exception with a message that names the layout element at fault.</summary>
    public SceneLayoutException(string message)
        : base(message)
    {
    }
}

