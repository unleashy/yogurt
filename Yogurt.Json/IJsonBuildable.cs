namespace Yogurt.Json;

public interface IJsonBuildable
{
    [PublicAPI]
    void ToJson(JsonBuilder json);
}
