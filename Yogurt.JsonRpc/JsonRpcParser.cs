namespace Yogurt.JsonRpc;

public static class JsonRpcParser
{
    public readonly struct Result
    {
        private readonly bool _isMessage;
        private readonly JsonRpcMessage _message;
        private readonly JsonRpcError _error;

        public Result(JsonRpcMessage message)
        {
            _isMessage = true;
            _message = message;
        }

        public Result(JsonRpcError error)
        {
            _isMessage = false;
            _error = error;
        }

        [PublicAPI] public JsonRpcMessage? Message => _isMessage ? _message : null;
        [PublicAPI] public JsonRpcError? Error => _isMessage ? null : _error;
    }

    [PublicAPI]
    public static Result Parse(ReadOnlyMemory<byte> input)
    {
        try {
            var json = JsonValue.Parse(input);

            if (IsResponse(json)) {
                var response = JsonRpcResponse.Parse(json);
                return new Result(new JsonRpcMessage(response));
            }
            else {
                var request = JsonRpcRequest.Parse(json);
                return new Result(new JsonRpcMessage(request));
            }
        }
        catch (JsonValueException e) {
            return new Result(
                new JsonRpcError(JsonRpcErrorCodes.InvalidRequest, e.Message)
            );
        }
        catch (JsonParseException e) {
            return new Result(new JsonRpcError(JsonRpcErrorCodes.ParseError, e.Message));
        }
    }

    private static bool IsResponse(in JsonValue json)
    {
        return json.TryObject() is {} o && o.Any(m => m.Key is "result" or "error");
    }
}
