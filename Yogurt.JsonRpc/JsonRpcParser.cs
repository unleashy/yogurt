namespace Yogurt.JsonRpc;

public static class JsonRpcParser
{
    public readonly struct Result
    {
        private readonly JsonRpcMessage _message;
        private readonly JsonRpcError _error;
        private readonly bool _isMessage;

        public static Result Message(in JsonRpcMessage message) =>
            new(isMessage: true, message: message);

        public static Result Error(in JsonRpcError error) =>
            new(isMessage: false, error: error);

        private Result(
            bool isMessage,
            JsonRpcMessage message = default,
            JsonRpcError error = default
        )
        {
            _isMessage = isMessage;
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
        try {
            var json = JsonValue.Parse(input);

            return Result.Message(JsonRpcMessage.Parse(json));
        }
        catch (JsonValueException e) {
            return Result.Error(new JsonRpcError(JsonRpcErrorCodes.InvalidRequest, e.Message));
        }
        catch (JsonParseException e) {
            return Result.Error(new JsonRpcError(JsonRpcErrorCodes.ParseError, e.Message));
        }
    }
}
