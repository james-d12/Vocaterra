using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vocaterra.Nlp.Tests;

/// <summary>
/// Two ONNX Runtime libraries in one test process. sherpa-onnx (the Kokoro speech tests) needs the libonnxruntime.so that its package ships;
/// Microsoft.ML.OnnxRuntime (the managed ONNX tests) needs its own, which has a different API version. Both files are named libonnxruntime.so,
/// so one output folder cannot hold both (the test projects used to be separate processes, each with one copy). The csproj puts the managed
/// package's copy in ort-managed/ and leaves the sherpa copy where sherpa's own package puts it. This module runs first: it loads the sherpa copy
/// by its full path, so the sherpa library links to that copy by its SONAME, and it maps the managed package's "onnxruntime" to the other copy.
/// </summary>
internal static class NativeOnnxRuntimes
{
    [ModuleInitializer]
    internal static void Install()
    {
        var sherpaCopy = Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "libonnxruntime.so");
        var managedCopy = Path.Combine(AppContext.BaseDirectory, "ort-managed", "libonnxruntime.so");
        if (!File.Exists(sherpaCopy) || !File.Exists(managedCopy))
        {
            return;
        }

        NativeLibrary.Load(sherpaCopy);
        NativeLibrary.SetDllImportResolver(
            typeof(Microsoft.ML.OnnxRuntime.SessionOptions).Assembly,
            (name, _, _) => name == "onnxruntime" ? NativeLibrary.Load(managedCopy) : IntPtr.Zero);
    }
}
