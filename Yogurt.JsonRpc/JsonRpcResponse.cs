using System.Diagnostics.CodeAnalysis;

namespace Yogurt.JsonRpc;

public readonly record struct JsonRpcResponse : IJsonable<JsonRpcResponse>
{
    private readonly bool _isResult = false;
    private readonly JsonValue _result = default;
    private readonly JsonRpcError _error = new();

    [PublicAPI] public required JsonRpcId Id { get; init; } = JsonRpcId.Null;
    [PublicAPI] public JsonValue? Result => _isResult ? _result : null;
    [PublicAPI] public JsonRpcError? Error => _isResult ? null : _error;

    [PublicAPI]
    public static JsonRpcResponse CreateResult(JsonRpcId id, JsonValue result) => new(id, result);

    [PublicAPI]
    public static JsonRpcResponse CreateError(JsonRpcId id, JsonRpcError error) => new(id, error);

    public JsonRpcResponse()
    {}

    [SetsRequiredMembers]
    private JsonRpcResponse(JsonRpcId id, JsonValue result)
    {
        _isResult = true;
        _result = result;
        Id = id;
    }

    [SetsRequiredMembers]
    private JsonRpcResponse(JsonRpcId id, JsonRpcError error)
    {
        _isResult = false;
        _error = error;
        Id = id;
    }

    [PublicAPI]
    public static JsonRpcResponse Parse(in JsonValue json) => json.Object(default, Shape);

    [PublicAPI]
    public void ToJson(JsonWriter json)
    {
        var self = this;

        json.Object(obj => {
            obj.Member("jsonrpc", it => it.String("2.0"));
            obj.Member("id", self.Id.ToJson);

            if (self.Result is {} result) obj.Member("result", result.ToJson);
            else if (self.Error is {} error) obj.Member("error", error.ToJson);
            else throw new System.Diagnostics.UnreachableException();
        });
    }

    private static readonly JsonObjectShape<JsonRpcResponse> Shape =
        new JsonObjectShape<JsonRpcResponse>()
            .Require("jsonrpc", static (in json) => json.Literal("2.0"))
            .Require("id", static (in json, res) => res with { Id = JsonRpcId.Parse(json) })
            .RequireOneOf(
                "result", static (in json, res) => CreateResult(res.Id, json),
                "error", static (in json, res) => CreateError(res.Id, JsonRpcError.Parse(json))
            );
}
