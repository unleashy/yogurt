namespace Yogurt.Json;

public interface IJsonObjectReader<T>
{
    [PublicAPI]
    bool TryRead(string key, in JsonValue value, scoped ref T state);

    [PublicAPI]
    bool Complete(
        IReadOnlySet<string> foundKeys,
        IReadOnlySet<string> rejectedKeys,
        scoped ref T state
    );
}
