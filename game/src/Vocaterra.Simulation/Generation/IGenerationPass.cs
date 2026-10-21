namespace Vocaterra.Simulation.Generation;

/// <summary>
/// One step of world generation. A pass reads and writes the grid through the context and draws randomness only from
/// <see cref="GenerationContext.Rng"/> and <see cref="GenerationContext.NoiseSeed"/>, with its own <see cref="Name"/>. Passes run in the
/// order of <see cref="WorldGenerator.DefaultPasses"/>; a new pass (caves, ores, vegetation) is a class added to that list.
/// </summary>
internal interface IGenerationPass
{
    /// <summary>The pass name; it is hashed into the pass's random streams, so renaming it changes the world.</summary>
    string Name { get; }

    /// <summary>Runs the pass.</summary>
    void Run(GenerationContext context);
}
