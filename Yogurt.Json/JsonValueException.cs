namespace Yogurt.Json;

[method: PublicAPI]
public sealed class JsonValueException(string message, string path, int line, int column)
    : Exception($"{path}: {message} (at {line}:{column})")
{
    [PublicAPI]
    public static JsonValueException Create(in JsonValue json, string message)
    {
        var (path, line, column) = json.GetLocationData();
        return new JsonValueException(message, path, line, column);
    }

    [PublicAPI] public string Path { get; } = path;
    [PublicAPI] public int Line { get; } = line;
    [PublicAPI] public int Column { get; } = column;
}
