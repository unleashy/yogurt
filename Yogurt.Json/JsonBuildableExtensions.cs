namespace Yogurt.Json;

public static class JsonBuildableExtensions
{
    extension<T>(T self)
        where T : IJsonBuildable, allows ref struct
    {
        [PublicAPI]
        public JsonValue ToJson()
        {
            var builder = new JsonBuilder();
            self.ToJson(builder);
            return builder.Build();
        }

        [PublicAPI]
        public ReadOnlyMemory<byte> ToJsonText()
        {
            return self.ToJson().Text;
        }
    }
}
