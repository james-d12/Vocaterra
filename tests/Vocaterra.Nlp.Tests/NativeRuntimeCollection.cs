namespace Vocaterra.Nlp.Tests;

/// <summary>
/// The xUnit collection of the tests that load a native speech or ONNX runtime (Vosk, sherpa-onnx, Kokoro, ONNX Runtime) and read the models
/// folder (VOCATERRA_MODELS_DIR) through the process-wide model gates. Each of them used to run in a test process of its own. In this one
/// assembly they share the runtimes' process-wide settings (Vosk's log level, the native libraries) and the gates' caches, so this collection
/// is not parallel: its tests run one at a time, and never beside another test of the assembly.
/// </summary>
[CollectionDefinition(NativeRuntimeCollection.Name, DisableParallelization = true)]
public static class NativeRuntimeCollection
{
    /// <summary>The collection's name, as the tests refer to it.</summary>
    public const string Name = "NativeRuntime";
}
