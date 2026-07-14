namespace Yogurt.JsonRpc;

public readonly record struct JsonRpcResponse : IJsonable<JsonRpcResponse>
{
    [PublicAPI] public JsonRpcId Id { get; init; }
    [PublicAPI] public JsonValue? Result { get; }
    [PublicAPI] public JsonRpcError? Error { get; }

    [PublicAPI]
    public static JsonRpcResponse CreateResult(JsonRpcId id, JsonValue result)
    {
        return new JsonRpcResponse(id, result);
    }

    [PublicAPI]
    public static JsonRpcResponse CreateError(JsonRpcId id, JsonRpcError error)
    {
        return new JsonRpcResponse(id, error);
    }

    private JsonRpcResponse(JsonRpcId id, JsonValue result)
    {
        Id = id;
        Result = result;
    }

    private JsonRpcResponse(JsonRpcId id, JsonRpcError error)
    {
        Id = id;
        Error = error;
    }

    [PublicAPI]
    public static JsonRpcResponse Parse(in JsonValue json) =>
        json.Object(new JsonRpcResponse(), Shape);

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
