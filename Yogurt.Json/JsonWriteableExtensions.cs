namespace Yogurt.Json;

public static class JsonWriteableExtensions
{
    [PublicAPI]
    public static JsonValue ToJson<T>(this T self)
        where T : IJsonWriteable, allows ref struct
    {
        var builder = new JsonBuilder();
        self.ToJson(builder);
        return builder.Build();
    }
}
