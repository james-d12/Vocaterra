using System.Text;

namespace Slg.Pronunciation.Tests;

/// <summary>
/// Builds a tiny ONNX model (Unsqueeze, Conv with a wav2vec2-like 400-sample window and 320-sample stride, Transpose) as raw protobuf
/// so the adapter's runtime plumbing can be tested without the 1 GB real model. Input <c>input_values</c> is float [1, samples];
/// output <c>logits</c> is float [1, frames, vocabulary].
/// </summary>
internal static class TinyOnnxModel
{
    private const int FloatType = 1;
    private const int Int64Type = 7;

    public static byte[] Build(int vocabularySize, int kernel = 400, int stride = 320)
    {
        var graph = new ProtoWriter();
        graph.Message(1, Node("Unsqueeze", ["input_values", "axes"], ["wave3d"]));
        graph.Message(1, Node("Conv", ["wave3d", "weights"], ["conv_out"], IntsAttribute("kernel_shape", kernel), IntsAttribute("strides", stride)));
        graph.Message(1, Node("Transpose", ["conv_out"], ["logits"], IntsAttribute("perm", 0, 2, 1)));
        graph.Text(2, "tiny-wav2vec2");
        graph.Message(5, Initializer("axes", Int64Type, [1], BitConverter.GetBytes(1L)));
        graph.Message(5, Initializer("weights", FloatType, [vocabularySize, 1, kernel], WeightBytes(vocabularySize, kernel)));
        graph.Message(11, ValueInfo("input_values", Dimension(1), Dimension("samples")));
        graph.Message(12, ValueInfo("logits", Dimension(1), Dimension("frames"), Dimension(vocabularySize)));

        var opset = new ProtoWriter();
        opset.Text(1, string.Empty);
        opset.Varint(2, 13);

        var model = new ProtoWriter();
        model.Varint(1, 7);
        model.Text(2, "slg-tests");
        model.Message(7, graph);
        model.Message(8, opset);
        return model.ToArray();
    }

    /// <summary>Deterministic small weights so different inputs give different logits.</summary>
    private static byte[] WeightBytes(int vocabularySize, int kernel)
    {
        var bytes = new byte[vocabularySize * kernel * sizeof(float)];
        for (var v = 0; v < vocabularySize; v++)
        {
            for (var k = 0; k < kernel; k++)
            {
                var value = (((v * 31) + (k * 17)) % 13 - 6) * 0.01f;
                BitConverter.GetBytes(value).CopyTo(bytes, ((v * kernel) + k) * sizeof(float));
            }
        }

        return bytes;
    }

    private static ProtoWriter Node(string opType, string[] inputs, string[] outputs, params ProtoWriter[] attributes)
    {
        var node = new ProtoWriter();
        foreach (var input in inputs)
        {
            node.Text(1, input);
        }

        foreach (var output in outputs)
        {
            node.Text(2, output);
        }

        node.Text(4, opType);
        foreach (var attribute in attributes)
        {
            node.Message(5, attribute);
        }

        return node;
    }

    private static ProtoWriter IntsAttribute(string name, params long[] values)
    {
        const int intsType = 7;
        var attribute = new ProtoWriter();
        attribute.Text(1, name);
        attribute.Varint(20, intsType);
        foreach (var value in values)
        {
            attribute.Varint(8, value);
        }

        return attribute;
    }

    private static ProtoWriter Initializer(string name, int dataType, long[] dims, byte[] raw)
    {
        var tensor = new ProtoWriter();
        foreach (var dim in dims)
        {
            tensor.Varint(1, dim);
        }

        tensor.Varint(2, dataType);
        tensor.Text(8, name);
        tensor.Bytes(9, raw);
        return tensor;
    }

    private static ProtoWriter Dimension(long value)
    {
        var dimension = new ProtoWriter();
        dimension.Varint(1, value);
        return dimension;
    }

    private static ProtoWriter Dimension(string parameter)
    {
        var dimension = new ProtoWriter();
        dimension.Text(2, parameter);
        return dimension;
    }

    private static ProtoWriter ValueInfo(string name, params ProtoWriter[] dimensions)
    {
        var shape = new ProtoWriter();
        foreach (var dimension in dimensions)
        {
            shape.Message(1, dimension);
        }

        var tensorType = new ProtoWriter();
        tensorType.Varint(1, FloatType);
        tensorType.Message(2, shape);

        var type = new ProtoWriter();
        type.Message(1, tensorType);

        var info = new ProtoWriter();
        info.Text(1, name);
        info.Message(2, type);
        return info;
    }

    private sealed class ProtoWriter
    {
        private readonly List<byte> bytes = [];

        public void Varint(int field, long value)
        {
            Tag(field, 0);
            WriteVarint((ulong)value);
        }

        public void Bytes(int field, byte[] data)
        {
            Tag(field, 2);
            WriteVarint((ulong)data.Length);
            bytes.AddRange(data);
        }

        public void Text(int field, string text)
        {
            Bytes(field, Encoding.UTF8.GetBytes(text));
        }

        public void Message(int field, ProtoWriter inner)
        {
            Bytes(field, inner.ToArray());
        }

        public byte[] ToArray()
        {
            return [.. bytes];
        }

        private void Tag(int field, int wireType)
        {
            WriteVarint((ulong)((field << 3) | wireType));
        }

        private void WriteVarint(ulong value)
        {
            while (value >= 0x80)
            {
                bytes.Add((byte)(value | 0x80));
                value >>= 7;
            }

            bytes.Add((byte)value);
        }
    }
}
