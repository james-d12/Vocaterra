namespace Vocaterra.Game.Core.Tip;

/// <summary>What a piece of the bilingual objective line is, so the HUD can colour it.</summary>
public enum ObjectivePieceKind
{
    /// <summary>The checklist's title (<c>Campamento</c>).</summary>
    Title,

    /// <summary>A target-language word of a step (<c>madera</c>).</summary>
    Word,

    /// <summary>A support-language word in brackets (<c>(wood)</c>), dropped once the noun is known; or a whole support-language line.</summary>
    Support,

    /// <summary>The count of a step (<c>0/5</c>).</summary>
    Count,

    /// <summary>The separator between steps and any plain text.</summary>
    Plain,
}
