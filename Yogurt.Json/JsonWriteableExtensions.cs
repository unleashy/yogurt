using System.Buffers;

namespace Yogurt.Json;

public static class JsonWriteableExtensions
{
    [PublicAPI]
    public static ReadOnlyMemory<byte> ToJson<T>(this T self)
        where T : IJsonWriteable, allows ref struct
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new JsonWriter(buffer);
        self.ToJson(writer);
        return buffer.WrittenMemory;
    }
}
