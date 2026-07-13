using System.Collections.Immutable;

namespace Yogurt.Json;

public sealed class JsonObjectShape<T> : IJsonObjectReader<T>
{
    public delegate void Inspector(in JsonValue json);
    public delegate T Parser(in JsonValue json, T state);

    private delegate void EntryReader(in JsonValue json, scoped ref T value);

    private readonly Dictionary<string, EntryReader> _entries = new();
    private readonly HashSet<string> _requiredKeys = new();
    private readonly Dictionary<string, HashSet<string>> _conflicts = new();

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
    public JsonObjectShape<T> RequireOneOf(
        string key1, Parser parser1,
        string key2, Parser parser2
    )
    {
        AddRequired(key1, (in json, scoped ref value) => value = parser1(json, value));
        AddRequired(key2, (in json, scoped ref value) => value = parser2(json, value));
        AddConflict(key1, key2);

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

    bool IJsonObjectReader<T>.TryRead(
        string key,
        in JsonValue value,
        JsonObjectReaderKeys keys,
        scoped ref T state
    )
    {
        if (_entries.TryGetValue(key, out var entry)) {
            var ckeys = GetConflicts(key);
            if (ckeys.Overlaps(keys.Found)) {
                var ckey = ckeys.Intersect(keys.Found).First();
                throw JsonValueException.Create(
                    value,
                    $"Invalid key {key.JsonEscape()} as it conflicts with {ckey.JsonEscape()}"
                );
            }

            entry(value, ref state);
        }
        else {
            throw JsonValueException.Create(value, $"Invalid key {key.JsonEscape()} in object");
        }

        return true;
    }

    bool IJsonObjectReader<T>.Complete(
        in JsonValue objectValue,
        JsonObjectReaderKeys keys,
        scoped ref T state
    )
    {
        if (HasMissingKeys(keys.Found)) {
            var missing = _requiredKeys.Except(keys.Found).Select(it => it.JsonEscape());
            throw JsonValueException.Create(
                objectValue,
                $"Missing required keys: {string.Join(", ", missing)}"
            );
        }

        return keys.Rejected.Count == 0 && keys.Found.IsSubsetOf(_entries.Keys);
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

    private void AddConflict(string key1, string key2)
    {
        if (!_conflicts.TryGetValue(key1, out var ckeys1)) {
            _conflicts[key1] = ckeys1 = new HashSet<string>();
        }

        if (!_conflicts.TryGetValue(key2, out var ckeys2)) {
            _conflicts[key2] = ckeys2 = new HashSet<string>();
        }

        _ = ckeys1.Add(key2);
        _ = ckeys2.Add(key1);
    }

    private IReadOnlySet<string> GetConflicts(string key)
    {
        return _conflicts.TryGetValue(key, out var ckeys)
            ? ckeys
            : ImmutableHashSet<string>.Empty;
    }

    private bool HasMissingKeys(IReadOnlySet<string> found)
    {
        return _requiredKeys.Except(found).Any(IsUnresolvedMissing);

        // If keys A and B are both required, but they conflict with each other, then, given key A
        // is present, ignore the requirement of B.
        bool IsUnresolvedMissing(string key) => !GetConflicts(key).Overlaps(found);
    }
}
