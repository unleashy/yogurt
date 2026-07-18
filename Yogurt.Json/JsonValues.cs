namespace Yogurt.Json;

public static class JsonValues
{
    [PublicAPI]
    public static JsonValue Null { get; } = CreateNullValue();

    [PublicAPI]
    public static JsonValue True { get; } = CreateBooleanValue(true);

    [PublicAPI]
    public static JsonValue False { get; } = CreateBooleanValue(false);

    private static JsonValue CreateBooleanValue(bool value)
    {
        var b = new JsonBuilder();
        b.Boolean(value);
        return b.Build();
    }

    private static JsonValue CreateNullValue()
    {
        var b = new JsonBuilder();
        b.Null();
        return b.Build();
    }
}
