using System.Diagnostics.CodeAnalysis;

namespace Yogurt.JsonRpc;

public readonly record struct JsonRpcResponse : IJsonable<JsonRpcResponse>
{
    [PublicAPI] public required JsonRpcId Id { get; init; } = JsonRpcId.Null;
    private readonly JsonValue _result;
    private readonly JsonRpcError _error;
    private readonly bool _isResult;

    [PublicAPI]
    public static JsonRpcResponse Result(JsonRpcId id, JsonValue result) => new(id, result);

    [PublicAPI]
    public static JsonRpcResponse Error(JsonRpcId id, JsonRpcError error) => new(id, error);

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
    public TOut Match<TOut>(Func<JsonValue, TOut> onValue, Func<JsonRpcError, TOut> onError)
    {
        if (_isResult) {
            return onValue(_result);
        }
        else {
            return onError(_error);
        }
    }

    [PublicAPI]
    public void Match(Action<JsonValue> onValue, Action<JsonRpcError> onError)
    {
        if (_isResult) {
            onValue(_result);
        }
        else {
            onError(_error);
        }
    }

    [PublicAPI]
    public JsonValue ToResult =>
        _isResult
            ? _result
            : throw new InvalidOperationException("Response is not a Result");

    [PublicAPI]
    public JsonRpcError ToError =>
        _isResult
            ? _error
            : throw new InvalidOperationException("Response is not an Error");

    [PublicAPI]
    public static JsonRpcResponse Parse(in JsonValue json) => json.Object(default, Shape);

    [PublicAPI]
    public void ToJson(JsonBuilder json)
    {
        var self = this;

        json.Object(obj => {
            obj.Member("jsonrpc", it => it.String("2.0"));
            obj.Member("id", self.Id.ToJson);

            self.Match(
                result => obj.Member("result", it => it.Value(result)),
                error => obj.Member("error", error.ToJson)
            );
        });
    }

    private static readonly JsonObjectShape<JsonRpcResponse> Shape =
        new JsonObjectShape<JsonRpcResponse>()
            .Require("jsonrpc", static (in json) => json.Literal("2.0"))
            .Require("id", static (in json, res) => res with { Id = JsonRpcId.Parse(json) })
            .RequireOneOf(
                "result", static (in json, res) => Result(res.Id, json),
                "error", static (in json, res) => Error(res.Id, JsonRpcError.Parse(json))
            );

    [PublicAPI]
    public override string ToString()
    {
        var id = Id;
        return Match(
            result => $"JsonRpcResponse {{ Id = {id}, Result = {result} }}",
            error => $"JsonRpcResponse {{ Id = {id}, Error = {error} }}"
        );
    }
}
