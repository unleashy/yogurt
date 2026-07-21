namespace Yogurt.JsonRpc;

public static class JsonRpcParser
{
    public readonly struct Result
    {
        [PublicAPI] public JsonRpcId? Id { get; }

        private readonly JsonRpcMessage _message;
        private readonly JsonRpcError _error;
        private readonly bool _isMessage;

        public static Result Message(in JsonRpcMessage message)
        {
            var id = message.Match(req => req.Id, res => res.Id);
            return new Result(isMessage: true, id, message: message);
        }

        public static Result Error(in JsonRpcId? id, in JsonRpcError error) =>
            new(isMessage: false, id, error: error);

        private Result(
            bool isMessage,
            JsonRpcId? id,
            JsonRpcMessage message = default,
            JsonRpcError error = default
        )
        {
            _isMessage = isMessage;
            Id = id;
            _message = message;
            _error = error;
        }

        [PublicAPI]
        public TOut Match<TOut>(Func<JsonRpcMessage, TOut> onValue, Func<JsonRpcError, TOut> onError)
        {
            if (_isMessage) {
                return onValue(_message);
            }
            else {
                return onError(_error);
            }
        }

        [PublicAPI]
        public void Match(Action<JsonRpcMessage> onValue, Action<JsonRpcError> onError)
        {
            if (_isMessage) {
                onValue(_message);
            }
            else {
                onError(_error);
            }
        }

        [PublicAPI]
        public JsonRpcMessage ToMessage =>
            _isMessage
                ? _message
                : throw new InvalidOperationException("Result is not a Message");

        [PublicAPI]
        public JsonRpcError ToError =>
            !_isMessage
                ? _error
                : throw new InvalidOperationException("Result is not an Error");
    }

    [PublicAPI]
    public static Result Parse(ReadOnlyMemory<byte> input)
    {
        // TODO: refactor this so i don't have to resort to exception-based control flow :(
        JsonValue json;
        try {
            json = JsonValue.Parse(input);
        }
        catch (JsonParseException e) {
            return Result.Error(null, new JsonRpcError(JsonRpcErrorCodes.ParseError, e.Message));
        }

        try {
            return Result.Message(JsonRpcMessage.Parse(json));
        }
        catch (JsonValueException e) {
            return Result.Error(
                TryExtractId(json),
                new JsonRpcError(JsonRpcErrorCodes.InvalidRequest, e.Message)
            );
        }
    }

    private static JsonRpcId? TryExtractId(in JsonValue json)
    {
        if (json.TryObject() is {} o) {
            foreach (var member in o) {
                if (member.Key == "id") {
                    return JsonRpcId.TryParse(member.Value);
                }
            }
        }

        return null;
    }
}
