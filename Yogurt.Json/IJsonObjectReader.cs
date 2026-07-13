namespace Yogurt.Json;

public interface IJsonObjectReader<T>
{
    [PublicAPI]
    bool TryRead(
        string key,
        in JsonValue value,
        JsonObjectReaderKeys keys,
        scoped ref T state
    );

    [PublicAPI]
    bool Complete(in JsonValue objectValue, JsonObjectReaderKeys keys, scoped ref T state);
}

[PublicAPI]
public readonly record struct JsonObjectReaderKeys(
    IReadOnlySet<string> Found,
    IReadOnlySet<string> Rejected
);
