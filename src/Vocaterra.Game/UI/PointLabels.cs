namespace Vocaterra.Game.UI;

/// <summary>
/// The Spanish word shown with the beacon for a named point of the scene (<c>river</c> shows <c>río</c>). These are provisional display words for
/// S2: S3 names the world's nouns in the content bundle (<c>río</c>, <c>casa</c>, <c>tienda</c>, <c>puente</c>) and S5 has them reviewed, and the
/// beacon then takes its word from the guide's answer. A point without an entry shows its own name.
/// </summary>
public static class PointLabels
{
    /// <summary>The label for a scene point name.</summary>
    public static string For(string pointName) => pointName switch
    {
        "river" => "río",
        "stall" => "tienda",
        "house1" or "house2" or "house3" => "casa",
        "bridge" => "puente",
        "workbench" => "mesa",
        "camp" or "camp.guide" => "campamento",
        "camp.player" => "mi campamento",
        "trail" => "camino",
        "lake" => "lago",
        "waterfall" => "cascada",
        "village" => "pueblo",
        "firering" => "fuego",
        "signpost" => "letrero",
        _ => pointName,
    };
}
