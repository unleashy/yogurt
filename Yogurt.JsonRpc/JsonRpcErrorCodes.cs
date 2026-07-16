namespace Yogurt.JsonRpc;

[PublicAPI]
public enum JsonRpcErrorCodes
{
    Unknown = -32001,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams = -32602,
    InternalError = -32603,
    ParseError = -32700,
}
