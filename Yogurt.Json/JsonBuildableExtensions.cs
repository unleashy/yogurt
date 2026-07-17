namespace Yogurt.Json;

public static class JsonBuildableExtensions
{
    [PublicAPI]
    public static JsonValue ToJson<T>(this T self)
        where T : IJsonBuildable, allows ref struct
    {
        var builder = new JsonBuilder();
        self.ToJson(builder);
        return builder.Build();
    }
}
