namespace Vocaterra.Game.Core.Npc;

/// <summary>When an NPC opens a talk with its opening host event (<see cref="NpcOpeningRule"/>).</summary>
public enum OpeningWhen
{
    /// <summary>Never: the text of the prompt the NPC waits on opens every talk (the guide).</summary>
    Never,

    /// <summary>At every opening, from any state (the villager's "Hola.").</summary>
    Always,

    /// <summary>At the first talk only, while the agent is still in its initial state (the shopkeeper's "¿Qué necesitas?").</summary>
    First,
}
