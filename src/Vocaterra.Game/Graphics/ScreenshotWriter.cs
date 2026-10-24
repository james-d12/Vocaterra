using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.Game.Graphics;

/// <summary>Saves the back buffer as a PNG, for the orchestrator's rendering check (<c>--screenshot</c>).</summary>
public static class ScreenshotWriter
{
    /// <summary>Reads the back buffer after the frame has been drawn and writes it to <paramref name="path"/>. Returns false (and says why on stderr) on failure.</summary>
    public static bool TrySave(GraphicsDevice device, string path)
    {
        try
        {
            var (data, width, height) = Capture(device);
            SaveImage(device, data, width, height, path);
            Console.WriteLine($"screenshot saved: {path} ({width} x {height})");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"screenshot failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Copies the back buffer into memory (S21: a report keeps its screenshot until it is saved, and a cancelled one writes nothing). Call it after the
    /// frame's world and layers are drawn and before anything else is drawn over them.
    /// </summary>
    public static (Microsoft.Xna.Framework.Color[] Pixels, int Width, int Height) Capture(GraphicsDevice device)
    {
        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;
        var data = new Microsoft.Xna.Framework.Color[width * height];
        device.GetBackBufferData(data);
        return (data, width, height);
    }

    /// <summary>Writes captured pixels as a PNG, creating the folder if it is not there.</summary>
    /// <param name="device">The graphics device (the texture is made on it).</param>
    /// <param name="pixels">The pixels from <see cref="Capture"/>.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="path">The PNG's path.</param>
    public static void SaveImage(GraphicsDevice device, Microsoft.Xna.Framework.Color[] pixels, int width, int height, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        using var stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
    }
}
