using Yogurt.Json;

namespace Yogurt.Server;

[PublicAPI]
public readonly record struct JsonRpcRequest(JsonRpcId? Id, string Method, JsonValue? Params)
{
    [PublicAPI]
    public static JsonRpcRequest? TryParse(JsonValue json) =>
        json.TryObject(
            new JsonRpcRequest(),
            new JsonObjectShape<JsonRpcRequest>()
                .Require("jsonrpc", static (in json) => json.TryLiteral("2.0"))
                .Require("method",
                    static (in json) => json.TryString(),
                    static (method, req) => req with { Method = method }
                )
                .Allow("id",
                    JsonRpcId.TryParse,
                    static (id, req) => req with { Id = id }
                )
                .Allow("params",
                    static (in json) => json.TryStructuralValue(),
                    static (@params, req) => req with { Params = @params }
                )
        );
}
