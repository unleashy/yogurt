using System.Buffers;

namespace Yogurt.Json;

public interface IJsonable<out TSelf>
    where TSelf : IJsonable<TSelf>
{
    [PublicAPI]
    public static abstract TSelf Parse(in JsonValue json);

    [PublicAPI]
    public void ToJson(JsonWriter json);
}

public static class JsonableExtensions
{
    public static ReadOnlyMemory<byte> ToJson<T>(this IJsonable<T> self)
        where T : IJsonable<T>
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new JsonWriter(buffer);
        self.ToJson(writer);
        return buffer.WrittenMemory;
    }
}
