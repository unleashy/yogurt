namespace Yogurt.Json;

public interface IJsonObjectReader<T>
{
    [PublicAPI]
    bool TryRead(JsonValue json, string key, scoped ref T value);

    [PublicAPI]
    bool Complete(IReadOnlySet<string> keysFound, scoped ref T value);
}
