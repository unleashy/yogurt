using Yogurt.Json;

namespace Yogurt.Server;

[PublicAPI]
public readonly record struct JsonRpcRequest(JsonRpcId? Id, string Method, JsonValue? Params)
{
    [PublicAPI]
    public static JsonRpcRequest Parse(JsonValue json) => json.Object(new JsonRpcRequest(), Shape);

    private static readonly JsonObjectShape<JsonRpcRequest> Shape =
        new JsonObjectShape<JsonRpcRequest>()
            .Require("jsonrpc", static (in json) => json.Literal("2.0"))
            .Require("method", static (in json, req) => req with { Method = json.String() })
            .Allow("id", static (in json, req) => req with { Id = JsonRpcId.Parse(json) })
            .Allow("params", static (in json, req) => req with { Params = json.StructuralValue() });
}
