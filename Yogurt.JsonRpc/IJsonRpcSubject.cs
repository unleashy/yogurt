namespace Yogurt.JsonRpc;

public interface IJsonRpcSubject
{
    [PublicAPI]
    ValueTask<JsonRpcMethodResult> InvokeAsync(
        string method,
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    ValueTask NotifyAsync(
        string method,
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    );
}
