namespace Yogurt.JsonRpc;

public readonly struct JsonRpcMessage
{
    private readonly bool _isResponse;
    private readonly JsonRpcRequest _request;
    private readonly JsonRpcResponse _response;

    [PublicAPI]
    public JsonRpcMessage(JsonRpcRequest request)
    {
        _isResponse = false;
        _request = request;
    }

    [PublicAPI]
    public JsonRpcMessage(JsonRpcResponse response)
    {
        _isResponse = true;
        _response = response;
    }

    [PublicAPI] public JsonRpcRequest? Request => _isResponse ? null : _request;
    [PublicAPI] public JsonRpcResponse? Response => _isResponse ? _response : null;
}
