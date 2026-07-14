namespace Yogurt.JsonRpc;

[method: PublicAPI]
public readonly record struct JsonRpcRequest(
    [property: PublicAPI] JsonRpcId? Id,
    [property: PublicAPI] string Method,
    [property: PublicAPI] JsonValue? Params
) : IJsonable<JsonRpcRequest>
{
    [PublicAPI]
    public static JsonRpcRequest Parse(in JsonValue json) =>
        json.Object(new JsonRpcRequest(), Shape);

    [PublicAPI]
    public void ToJson(JsonWriter json)
    {
        var self = this;

        json.Object(obj => {
            obj.Member("jsonrpc", it => it.String("2.0"));
            obj.Member("method", it => it.String(self.Method));
            if (self.Id is {} id) obj.Member("id", id.ToJson);
            if (self.Params is {} p) obj.Member("params", p.ToJson);
        });
    }

    private static readonly JsonObjectShape<JsonRpcRequest> Shape =
        new JsonObjectShape<JsonRpcRequest>()
            .Require("jsonrpc", static (in json) => json.Literal("2.0"))
            .Require("method", static (in json, req) => req with { Method = json.String() })
            .Allow("id", static (in json, req) => req with { Id = JsonRpcId.Parse(json) })
            .Allow("params", static (in json, req) => req with { Params = json.StructuralValue() });
}
