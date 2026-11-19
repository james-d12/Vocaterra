namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>The HUD lines of wear (I07): the key of each, a row of <c>hudLines</c> in <c>game/words.json</c> (the Spanish the player reads) and of the English locale under the same id.</summary>
public static class WearText
{
    /// <summary>The line said once when a tool wears out (<c>tool_worn</c>): «Necesito arreglar la herramienta.»</summary>
    public const string ToolWorn = "wear.toolWorn";

    /// <summary>Every key, each needing a Spanish HUD line and its English (<see cref="Content.GameDataValidator.HudLineKeys"/>).</summary>
    public static IReadOnlyList<string> AllKeys { get; } = [ToolWorn];
}
