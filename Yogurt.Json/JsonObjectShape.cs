namespace Yogurt.Json;

public sealed class JsonObjectShape<T> : IJsonObjectReader<T>
{
    public delegate void Inspector(in JsonValue json);
    public delegate T Parser(in JsonValue json, T state);

    private delegate void EntryReader(in JsonValue json, scoped ref T value);

    private readonly Dictionary<string, EntryReader> _entries = new();
    private readonly HashSet<string> _requiredKeys = new();

    [PublicAPI]
    public JsonObjectShape<T> Require(string key, Inspector inspector)
    {
        AddRequired(key, (in json, scoped ref _) => inspector(json));
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Require(string key, Parser parser)
    {
        AddRequired(key, (in json, scoped ref value) => value = parser(json, value));
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Allow(string key, Inspector inspector)
    {
        AddOptional(key, (in json, scoped ref _) => inspector(json));
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Allow(string key, Parser parser)
    {
        AddOptional(key, (in json, scoped ref value) => value = parser(json, value));
        return this;
    }

    private void AddRequired(string key, EntryReader reader)
    {
        _entries.Add(key, reader);
        _ = _requiredKeys.Add(key);
    }

    private void AddOptional(string key, EntryReader reader)
    {
        _entries.Add(key, reader);
    }

    bool IJsonObjectReader<T>.TryRead(string key, in JsonValue value, scoped ref T state)
    {
        if (_entries.TryGetValue(key, out var reader)) {
            reader(value, ref state);
        }
        else {
            throw JsonValueException.Create(value, $"Invalid key {key.JsonEscape()} in object");
        }

        return true;
    }

    bool IJsonObjectReader<T>.Complete(
        in JsonValue objectValue,
        IReadOnlySet<string> foundKeys,
        IReadOnlySet<string> rejectedKeys,
        scoped ref T state
    )
    {
        var hadAllRequiredKeys = _requiredKeys.IsSubsetOf(foundKeys);
        if (!hadAllRequiredKeys) {
            var missing = _requiredKeys.Except(foundKeys).Select(it => it.JsonEscape());
            throw JsonValueException.Create(
                objectValue,
                $"Missing required keys: {string.Join(", ", missing)}"
            );
        }

        return rejectedKeys.Count == 0 && hadAllRequiredKeys && foundKeys.IsSubsetOf(_entries.Keys);
    }
}
