namespace Yogurt.JsonRpc;

public sealed class JsonRpcServer(IJsonRpcChannel channel, IJsonRpcSubject subject)
    : IJsonRpcObserver
{
    [PublicAPI]
    public ValueTask<bool> OnRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled<bool>(cancellationToken);
        }

        if (request.Id is {} id) {
            return InvokeAsyncCore(id, request, cancellationToken);
        }
        else {
            return NotifyAsyncCore(request, cancellationToken);
        }
    }

    private async ValueTask<bool> InvokeAsyncCore(
        JsonRpcId id,
        JsonRpcRequest request,
        CancellationToken ct
    )
    {
        var result = await subject.InvokeAsync(request.Method, request.Params, ct);

        await result.Match(
            value => SendResult(id, value, ct),
            error => SendError(id, error, ct)
        );

        return true;
    }
    
    private async ValueTask<bool> NotifyAsyncCore(JsonRpcRequest request, CancellationToken ct)
    {
        await subject.NotifyAsync(request.Method, request.Params, ct);

        return true;
    }

    private ValueTask SendResult(
        JsonRpcId id,
        JsonValue result,
        CancellationToken cancellationToken
    )
    {
        return channel.Output.WriteAsync(
            JsonRpcMessage.Response(JsonRpcResponse.Result(id, result)),
            cancellationToken
        );
    }

    private ValueTask SendError(
        JsonRpcId? id,
        JsonRpcError error,
        CancellationToken cancellationToken
    )
    {
        return channel.Output.WriteAsync(
            JsonRpcMessage.Response(
                JsonRpcResponse.Error(id ?? JsonRpcId.Null, error)
            ),
            cancellationToken
        );
    }

    ValueTask<bool> IJsonRpcObserver.OnResponseAsync(
        JsonRpcResponse response,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(false);
    }
}
