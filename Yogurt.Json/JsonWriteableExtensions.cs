namespace Yogurt.Json;

public static class JsonWriteableExtensions
{
    [PublicAPI]
    public static JsonValue ToJson<T>(this T self)
        where T : IJsonWriteable, allows ref struct
    {
        var writer = new JsonWriter();
        self.ToJson(writer);
        return writer.DrainToJson();
    }
}
