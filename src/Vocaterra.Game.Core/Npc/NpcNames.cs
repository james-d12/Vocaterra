namespace Vocaterra.Game.Core.Npc;

/// <summary>The NPCs' names as the player reads them, over their heads and on the dialogue panel.</summary>
public static class NpcNames
{
    /// <summary>The Spanish name shown over an NPC.</summary>
    public static string LabelOf(string npcName) => npcName switch
    {
        "guide" => "Guía",
        "shopkeeper" => "Tendero",
        "villager" => "Aldeano",
        _ => npcName,
    };
}
