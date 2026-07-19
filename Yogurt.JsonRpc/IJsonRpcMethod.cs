namespace Yogurt.JsonRpc;

public interface IJsonRpcMethod
{
    [PublicAPI]
    ValueTask<JsonRpcMethodResult> InvokeAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    ValueTask NotifyAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    );
}
