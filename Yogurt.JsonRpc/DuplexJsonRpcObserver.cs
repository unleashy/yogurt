namespace Yogurt.JsonRpc;

public sealed class DuplexJsonRpcObserver(JsonRpcClient client, JsonRpcServer server)
    : IJsonRpcObserver
{
    public ValueTask<bool> OnRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return server.OnRequestAsync(request, cancellationToken);
    }

    public ValueTask<bool> OnResponseAsync(
        JsonRpcResponse response,
        CancellationToken cancellationToken = default
    )
    {
        return client.OnResponseAsync(response, cancellationToken);
    }

    public void OnComplete()
    {
        client.OnComplete();
        server.OnComplete();
    }
}
