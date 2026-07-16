using System.Diagnostics.CodeAnalysis;

namespace Yogurt.JsonRpc;

public readonly record struct JsonRpcError : IJsonable<JsonRpcError>
{
    [PublicAPI] public required int Code { get; init; } = (int)JsonRpcErrorCodes.Unknown;
    [PublicAPI] public required string Message { get; init; } = "Unknown error";
    [PublicAPI] public JsonValue? Data { get; init; } = null;

    [PublicAPI, SetsRequiredMembers]
    public JsonRpcError()
    {}

    [PublicAPI]
    public static JsonRpcError Parse(in JsonValue json) => json.Object(Shape);

    [PublicAPI]
    public void ToJson(JsonWriter json)
    {
        var self = this;

        json.Object(obj => {
            obj.Member("code", it => it.Number(self.Code));
            obj.Member("message", it => it.String(self.Message));
            if (self.Data is {} data) obj.Member("data", it => it.Value(data));
        });
    }

    private static readonly JsonObjectShape<JsonRpcError> Shape =
        new JsonObjectShape<JsonRpcError>()
            .Require("code", static (in json, err) => err with { Code = json.Number<int>() })
            .Require("message", static (in json, err) => err with { Message = json.String() })
            .Allow("data", static (in json, err) => err with { Data = json });
}
