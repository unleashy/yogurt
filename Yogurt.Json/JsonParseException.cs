namespace Yogurt.Json;

public sealed class JsonParseException(string message, int line, int column) : Exception(message)
{
    [PublicAPI] public int Line { get; } = line;
    [PublicAPI] public int Column { get; } = column;
}
