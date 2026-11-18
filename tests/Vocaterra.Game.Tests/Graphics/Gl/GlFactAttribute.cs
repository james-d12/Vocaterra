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
    private static readonly Lazy<GlProbeGame> SharedGame = new(static () =>
    {
        var game = new GlProbeGame();
        game.RunOneFrame();
        return game;
    });

    /// <summary>The one game (and so the one window and context) every live test shares: a third window in a process does not open (the test host hangs), so the live tests take turns on this one and leave its state as they found it.</summary>
    public static GlProbeGame Shared => SharedGame.Value;

    private readonly Microsoft.Xna.Framework.GraphicsDeviceManager _graphics;

    public GlProbeGame()
    {
        _graphics = new Microsoft.Xna.Framework.GraphicsDeviceManager(this) { PreferredBackBufferWidth = 64, PreferredBackBufferHeight = 64 };
    }

    public Microsoft.Xna.Framework.GraphicsDeviceManager Graphics => _graphics;
}
