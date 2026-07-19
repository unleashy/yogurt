namespace Yogurt.JsonRpc;

public readonly struct JsonRpcMethodResult
{
    private readonly bool _isResult;
    private readonly JsonValue _result;
    private readonly JsonRpcError _error;

    [PublicAPI]
    public static JsonRpcMethodResult Ok(JsonValue value) =>
        new(isResult: true, result: value);

    [PublicAPI]
    public static JsonRpcMethodResult Error(JsonRpcError error) =>
        new(isResult: false, error: error);

    private JsonRpcMethodResult(
        bool isResult,
        JsonValue result = default,
        JsonRpcError error = default
    )
    {
        _isResult = isResult;
        _result = result;
        _error = error;
    }

    [PublicAPI]
    public TOut Match<TOut>(Func<JsonValue, TOut> onResult, Func<JsonRpcError, TOut> onError)
    {
        return _isResult ? onResult(_result) : onError(_error);
    }

    [PublicAPI]
    public JsonValue ToValue =>
        _isResult
            ? _result
            : throw new InvalidOperationException("Result is an error");

    [PublicAPI]
    public JsonRpcError ToError =>
        !_isResult
            ? _error
            : throw new InvalidOperationException("Result is a value");
}
