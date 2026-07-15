namespace Yogurt.Json;

public interface IJsonParseable<out TSelf>
    where TSelf : IJsonParseable<TSelf>
{
    [PublicAPI]
    static abstract TSelf Parse(in JsonValue json);
}
