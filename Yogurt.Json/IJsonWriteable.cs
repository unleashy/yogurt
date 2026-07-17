namespace Yogurt.Json;

public interface IJsonWriteable
{
    [PublicAPI]
    public void ToJson(JsonBuilder json);
}
