namespace Slg.Core.Prompts;

/// <summary>Whether recognition is limited to a closed phrase set or open vocabulary.</summary>
public enum RecognitionMode
{
    /// <summary>Recognition is constrained to the grammar phrases of the prompt.</summary>
    Constrained,

    /// <summary>Open-vocabulary recognition.</summary>
    Open,
}
