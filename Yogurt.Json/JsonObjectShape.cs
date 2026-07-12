namespace Yogurt.Json;

public sealed class JsonObjectShape<T> : IJsonObjectReader<T>
{
    public delegate TValue? ValueParser<TValue>(in JsonValue json) where TValue : struct;
    public delegate TValue? Parser<out TValue>(in JsonValue json) where TValue : class;

    private delegate bool EntryReader(in JsonValue json, scoped ref T value);

    private readonly Dictionary<string, EntryReader> _entries = new();
    private readonly HashSet<string> _requiredKeys = new();

    [PublicAPI]
    public JsonObjectShape<T> Require<TValue>(string key, Parser<TValue> inspector)
        where TValue : class
    {
        AddRequired(key, (in json, scoped ref _) => inspector(json) is not null);
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Require<TValue>(string key, ValueParser<TValue> inspector)
        where TValue : struct
    {
        AddRequired(key, (in json, scoped ref _) => inspector(json) is not null);
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Require<TValue>(
        string key,
        Parser<TValue> parser,
        Func<TValue, T, T> joiner
    )
        where TValue : class
    {
        AddRequired(key, (in json, scoped ref value) => {
            if (parser(json) is {} result) {
                value = joiner(result, value);
                return true;
            }
            else {
                return false;
            }
        });

        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Require<TValue>(
        string key,
        ValueParser<TValue> parser,
        Func<TValue, T, T> joiner
    )
        where TValue : struct
    {
        AddRequired(key, (in json, scoped ref value) => {
            if (parser(json) is {} result) {
                value = joiner(result, value);
                return true;
            }
            else {
                return false;
            }
        });

        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Allow<TValue>(string key, Parser<TValue> inspector)
        where TValue : class
    {
        AddOptional(key, (in json, scoped ref _) => inspector(json) is not null);
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Allow<TValue>(string key, ValueParser<TValue> inspector)
        where TValue : struct
    {
        AddOptional(key, (in json, scoped ref _) => inspector(json) is not null);
        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Allow<TValue>(
        string key,
        Parser<TValue> parser,
        Func<TValue, T, T> joiner
    )
        where TValue : class
    {
        AddOptional(key, (in json, scoped ref value) => {
            if (parser(json) is {} result) {
                value = joiner(result, value);
                return true;
            }
            else {
                return false;
            }
        });

        return this;
    }

    [PublicAPI]
    public JsonObjectShape<T> Allow<TValue>(
        string key,
        ValueParser<TValue> parser,
        Func<TValue, T, T> joiner
    )
        where TValue : struct
    {
        AddOptional(key, (in json, scoped ref value) => {
            if (parser(json) is {} result) {
                value = joiner(result, value);
                return true;
            }
            else {
                return false;
            }
        });

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
        return _entries.TryGetValue(key, out var reader) && reader(value, ref state);
    }

    bool IJsonObjectReader<T>.Complete(
        IReadOnlySet<string> foundKeys,
        IReadOnlySet<string> rejectedKeys,
        scoped ref T state
    )
    {
        return
            rejectedKeys.Count == 0
            && _requiredKeys.IsSubsetOf(foundKeys)
            && foundKeys.IsSubsetOf(_entries.Keys);
    }
}
