namespace Yogurt.JsonRpc;

[method: PublicAPI]
public readonly record struct JsonRpcError(
    [property: PublicAPI] int Code,
    [property: PublicAPI] string Message,
    [property: PublicAPI] JsonValue? Data
) :
    IJsonable<JsonRpcError>
{
    [PublicAPI]
    public static JsonRpcError Parse(in JsonValue json) =>
        json.Object(new JsonRpcError(), Shape);

    [PublicAPI]
    public void ToJson(JsonWriter json)
    {
        var self = this;

        json.Object(obj => {
            obj.Member("code", it => it.Number(self.Code));
            obj.Member("message", it => it.String(self.Message));
            if (self.Data is {} data) obj.Member("data", data.ToJson);
        });
    }

    private static readonly JsonObjectShape<JsonRpcError> Shape =
        new JsonObjectShape<JsonRpcError>()
            .Require("code", static (in json, err) => err with { Code = json.Number<int>() })
            .Require("message", static (in json, err) => err with { Message = json.String() })
            .Allow("data", static (in json, err) => err with { Data = json });
}
