using Yogurt.Json;

namespace Yogurt.Server;

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
    public static JsonRpcId Parse(in JsonValue json) =>
        json.TryNumber<int>() is {} ival ? JsonRpcId.Int(ival)
        : json.TryString() is {} sval ? JsonRpcId.String(sval)
        : json.TryNull() ? JsonRpcId.Null
        : throw JsonValueException.Create(
              json,
              $"Expected a number parseable as Int, string, or null; got {json.HumanTypeName()}"
          );

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
