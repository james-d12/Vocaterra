namespace Vocaterra.Nlp.Tests;

/// <summary>
/// The xUnit collection of the tests that load a native speech or ONNX runtime (Vosk, sherpa-onnx, Kokoro, ONNX Runtime) and read the models
/// folder (VOCATERRA_MODELS_DIR) through the process-wide model gates. Each of them used to run in a test process of its own. In this one
/// assembly they share the runtimes' process-wide settings (Vosk's log level, the native libraries) and the gates' caches, so they are one
/// collection and its tests run one at a time. The collection runs beside the assembly's other tests, none of which loads a native runtime; a
/// test that does belongs here. (Until 2026-10-29 it also waited for every other test of the assembly; owner, docs/process/test-performance-plan.md T3b.)
/// </summary>
[CollectionDefinition(NativeRuntimeCollection.Name)]
public static class NativeRuntimeCollection
{
    /// <summary>The collection's name, as the tests refer to it.</summary>
    public const string Name = "NativeRuntime";
}
