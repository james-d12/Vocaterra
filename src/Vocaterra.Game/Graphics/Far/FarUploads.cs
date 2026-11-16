namespace Vocaterra.Game.Graphics.Far;

/// <summary>Which of the tiles whose meshes wait for the card go up in a frame (B07c): new tiles before tiles made again, the coarsest level first (a few big tiles are the whole horizon), nearest first within a level (the draw list's order), until the byte budget is spent; always at least one, so nothing waits for ever.</summary>
public static class FarUploads
{
    /// <summary>Chooses the tiles to upload this frame.</summary>
    /// <param name="draw">The draw list, in order.</param>
    /// <param name="topLevel">The coarsest level.</param>
    /// <param name="budgetBytes">The bytes of meshes a frame may hand over.</param>
    /// <param name="into">Receives the chosen tiles, in upload order (cleared first).</param>
    public static void Choose(IReadOnlyList<FarTile> draw, int topLevel, long budgetBytes, List<FarTile> into)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        var pending = false;
        for (var i = 0; i < draw.Count && !pending; i++)
        {
            pending = draw[i].Pending is not null;
        }

        if (!pending)
        {
            return;
        }

        var budget = budgetBytes;
        for (var replacing = 0; replacing < 2; replacing++)
        {
            for (var level = topLevel; level >= 0; level--)
            {
                for (var i = 0; i < draw.Count; i++)
                {
                    var tile = draw[i];
                    if (tile.Pending is not { } mesh || tile.Key.Level != level || (tile.Buffer is not null) != (replacing == 1))
                    {
                        continue;
                    }

                    if (into.Count > 0 && budget <= 0)
                    {
                        return;
                    }

                    budget -= mesh.Bytes;
                    into.Add(tile);
                }
            }
        }
    }
}
