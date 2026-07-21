namespace Yogurt.JsonRpc;

public interface IJsonRpcObserver
{
    [PublicAPI]
    ValueTask<bool> OnRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    ValueTask<bool> OnResponseAsync(
        JsonRpcResponse response,
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    void OnComplete();
}
