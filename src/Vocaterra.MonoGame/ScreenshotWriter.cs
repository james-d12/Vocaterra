using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.MonoGame;

/// <summary>Saves the back buffer as a PNG, for the orchestrator's rendering check (<c>--screenshot</c>).</summary>
public static class ScreenshotWriter
{
    /// <summary>Reads the back buffer after the frame has been drawn and writes it to <paramref name="path"/>. Returns false (and says why on stderr) on failure.</summary>
    public static bool TrySave(GraphicsDevice device, string path)
    {
        try
        {
            var width = device.PresentationParameters.BackBufferWidth;
            var height = device.PresentationParameters.BackBufferHeight;
            var data = new Microsoft.Xna.Framework.Color[width * height];
            device.GetBackBufferData(data);
            using var texture = new Texture2D(device, width, height);
            texture.SetData(data);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Create(path);
            texture.SaveAsPng(stream, width, height);
            Console.WriteLine($"screenshot saved: {path} ({width} x {height})");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"screenshot failed: {ex.Message}");
            return false;
        }
    }
}
