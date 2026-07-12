namespace Yogurt.Json;

[method: PublicAPI]
public sealed class JsonParseException(string message, int line, int column)
    : Exception($"{message} ({line}:{column})")
{
    [PublicAPI] public int Line { get; } = line;
    [PublicAPI] public int Column { get; } = column;
}
