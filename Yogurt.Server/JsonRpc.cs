using Yogurt.Json;

namespace Yogurt.Server;

public static class JsonRpc
{
    [PublicAPI]
    public static JsonRpcRequest? TryParseRequest(JsonValue json) =>
        json.TryObject(
            new JsonRpcRequest(),
            new JsonObjectShape<JsonRpcRequest>()
                .Require("jsonrpc", static json => json.TryLiteral("2.0"))
                .Require("method",
                    static json => json.TryString(),
                    static (method, req) => req with { Method = method }
                )
                .Allow("id",
                    TryParseId,
                    static (id, req) => req with { Id = id }
                )
                .Allow("params",
                    static json => json.TryStructuralValue(),
                    static (@params, req) => req with { Params = @params }
                )
        );

    private static JsonRpcId? TryParseId(JsonValue json) =>
        json.TryNumber<int>() is {} ival ? JsonRpcId.Int(ival)
        : json.TryString() is {} sval ? JsonRpcId.String(sval)
        : json.TryNull() ? JsonRpcId.Null
        : null;
}

public readonly struct JsonRpcId : IEquatable<JsonRpcId>
{
    private enum Repr
    {
        Null,
        Integer,
        String,
    }

    private readonly Repr _repr = Repr.Null;
    private readonly int _ival = 0;
    private readonly string? _sval = null;

    [PublicAPI]
    public static readonly JsonRpcId Null;

    [PublicAPI]
    public static JsonRpcId Int(int id) => new(id);

    [PublicAPI]
    public static JsonRpcId String(string id) => new(id);

    private JsonRpcId(int id)
    {
        _repr = Repr.Integer;
        _ival = id;
    }

    private JsonRpcId(string id)
    {
        _repr = Repr.String;
        _sval = id;
    }

    [PublicAPI]
    public bool Equals(JsonRpcId other) => (_repr, other._repr) switch {
        (Repr.Null,    Repr.Null)    => true,
        (Repr.Integer, Repr.Integer) => _ival == other._ival,
        (Repr.String,  Repr.String)  => _sval == other._sval,

        _ => false,
    };

    [PublicAPI]
    public override bool Equals(object? obj) => obj is JsonRpcId other && Equals(other);

    [PublicAPI]
    public override int GetHashCode() => _repr switch {
        Repr.Null    => HashCode.Combine(_repr),
        Repr.Integer => HashCode.Combine(_repr, _ival),
        Repr.String  => HashCode.Combine(_repr, _sval),
    };

    [PublicAPI]
    public static bool operator ==(JsonRpcId left, JsonRpcId right) => left.Equals(right);

    [PublicAPI]
    public static bool operator !=(JsonRpcId left, JsonRpcId right) => !left.Equals(right);

    [PublicAPI]
    public override string ToString() => _repr switch {
        Repr.Null    => "JsonRpcId(null)",
        Repr.Integer => $"JsonRpcId({_ival})",
        Repr.String  => $"JsonRpcId({_sval})",
    };
}

[PublicAPI]
public readonly record struct JsonRpcRequest(JsonRpcId? Id, string Method, JsonValue? Params);
