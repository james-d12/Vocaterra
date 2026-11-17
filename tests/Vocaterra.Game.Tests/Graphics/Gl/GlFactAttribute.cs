namespace Vocaterra.Game.Tests.Graphics.Gl;

/// <summary>A fact that needs an OpenGL context (MonoGame opens a window for a frame): skipped unless <c>VOCATERRA_GL_TESTS=1</c> and a display is there.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GlFactAttribute : FactAttribute
{
    /// <summary>Creates the fact.</summary>
    public GlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VOCATERRA_GL_TESTS") != "1")
        {
            Skip = "No OpenGL context in the gate: set VOCATERRA_GL_TESTS=1 (and DISPLAY) to run it.";
        }
    }
}

/// <summary>A MonoGame game that only makes a device (and so a context) for the GL tests.</summary>
internal sealed class GlProbeGame : Microsoft.Xna.Framework.Game
{
    private readonly Microsoft.Xna.Framework.GraphicsDeviceManager _graphics;

    public GlProbeGame()
    {
        _graphics = new Microsoft.Xna.Framework.GraphicsDeviceManager(this) { PreferredBackBufferWidth = 64, PreferredBackBufferHeight = 64 };
    }

    public Microsoft.Xna.Framework.GraphicsDeviceManager Graphics => _graphics;
}
