namespace Yogurt.JsonRpc;

public readonly struct JsonRpcMessage : IJsonParseable<JsonRpcMessage>
{
    private readonly JsonRpcRequest _request;
    private readonly JsonRpcResponse _response;
    private readonly bool _isResponse;

    [PublicAPI]
    public static JsonRpcMessage Request(in JsonRpcRequest request) =>
        new(isResponse: false, request: request);

    [PublicAPI]
    public static JsonRpcMessage Response(in JsonRpcResponse response) =>
        new(isResponse: true, response: response);

    [PublicAPI]
    public static JsonRpcMessage Parse(in JsonValue json)
    {
        string? version = null;
        JsonRpcId? id = null;

        string? method = null;
        JsonValue? @params = null;

        JsonValue? result = null;
        JsonRpcError? error = null;

        foreach (var member in json.Object()) {
            var (key, value) = member;

            switch (key) {
                case "jsonrpc": {
                    Dedupe(ref version, member) = value.Literal("2.0");
                    break;
                }

                case "id": {
                    Dedupe(ref id, member) = JsonRpcId.Parse(value);
                    break;
                }

                case "method": {
                    ExpectRequest(member);
                    Dedupe(ref method, member) = value.String();
                    break;
                }

                case "params": {
                    ExpectRequest(member);
                    Dedupe(ref @params, member) = value.StructuralValue();
                    break;
                }

                case "result": {
                    ExpectResponse(member);
                    Dedupe(ref result, member) = value;
                    break;
                }

                case "error": {
                    ExpectResponse(member);
                    Dedupe(ref error, member) = JsonRpcError.Parse(value);
                    break;
                }

                default: {
                    throw Invalid(member);
                }
            }
        }

        if (version is null) {
            throw JsonValueException.Create(json, "Missing required keys: \"jsonrpc\"");
        }

        if (method is {} m) {
            return JsonRpcMessage.Request(new JsonRpcRequest(id, m, @params));
        }
        else if (@params is not null) {
            throw JsonValueException.Create(json, "Missing required keys: \"method\"");
        }
        else if (result is {} r) {
            return JsonRpcMessage.Response(JsonRpcResponse.Result(RequireId(json), r));
        }
        else if (error is {} e) {
            return JsonRpcMessage.Response(JsonRpcResponse.Error(RequireId(json), e));
        }
        else {
            throw JsonValueException.Create(json, "Invalid object: not a Request or a Response");
        }

        JsonRpcId RequireId(in JsonValue json)
        {
            if (id is {} it) {
                return it;
            }
            else {
                throw JsonValueException.Create(json, "Missing required keys: \"id\"");
            }
        }

        void ExpectRequest(in KeyValuePair<string, JsonValue> member)
        {
            if (result is not null) {
                throw Conflict(member, "result");
            }

            if (error is not null) {
                throw Conflict(member, "error");
            }
        }

        void ExpectResponse(in KeyValuePair<string, JsonValue> member)
        {
            if (method is not null) {
                throw Conflict(member, "method");
            }

            if (@params is not null) {
                throw Conflict(member, "params");
            }

            if (result is null && error is not null) {
                throw Conflict(member, "result");
            }

            if (result is not null && error is null) {
                throw Conflict(member, "error");
            }
        }

        static ref T? Dedupe<T>(ref T? ptr, in KeyValuePair<string, JsonValue> member)
        {
            if (ptr is null) {
                return ref ptr;
            }
            else {
                throw Duplicate(member);
            }
        }

        static JsonValueException Conflict(
            in KeyValuePair<string, JsonValue> member,
            string conflictKey
        ) =>
            JsonValueException.Create(
                member.Value,
                $"Invalid key \"{member.Key}\" as it conflicts with \"{conflictKey}\""
            );

        static JsonValueException Duplicate(in KeyValuePair<string, JsonValue> member) =>
            JsonValueException.Create(
                member.Value,
                $"Unexpected duplicate key \"{member.Key}\" in object"
            );

        static JsonValueException Invalid(in KeyValuePair<string, JsonValue> member) =>
            JsonValueException.Create(
                member.Value,
                $"Invalid key \"{member.Key}\" in object"
            );
    }

    private JsonRpcMessage(
        bool isResponse,
        in JsonRpcRequest request = default,
        in JsonRpcResponse response = default
    )
    {
        _isResponse = isResponse;
        _request = request;
        _response = response;
    }

    [PublicAPI]
    public TOut Match<TOut>(
        Func<JsonRpcRequest, TOut> onRequest,
        Func<JsonRpcResponse, TOut> onResponse
    )
    {
        if (_isResponse) {
            return onResponse(_response);
        }
        else {
            return onRequest(_request);
        }
    }

    [PublicAPI]
    public void Match(Action<JsonRpcRequest> onRequest, Action<JsonRpcResponse> onResponse)
    {
        if (_isResponse) {
            onResponse(_response);
        }
        else {
            onRequest(_request);
        }
    }

    [PublicAPI]
    public JsonRpcRequest ToRequest =>
        !_isResponse
            ? _request
            : throw new InvalidOperationException("Message is not a Request");

    [PublicAPI]
    public JsonRpcResponse ToResponse =>
        _isResponse
            ? _response
            : throw new InvalidOperationException("Message is not a Response");
}
