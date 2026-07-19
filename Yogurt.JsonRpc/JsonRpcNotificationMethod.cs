namespace Yogurt.JsonRpc;

public abstract class JsonRpcNotificationMethod<TParams> : IJsonRpcMethod
    where TParams : IJsonParseable<TParams>
{
    [PublicAPI]
    public abstract ValueTask NotifyAsync(
        TParams parameters,
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    public virtual ValueTask NotifyAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled(cancellationToken);
        }

        TParams req;
        try {
            req = TParams.Parse(parameters ?? JsonValues.Null);
        }
        catch (JsonValueException) {
            return ValueTask.CompletedTask;
        }

        return NotifyAsync(req, cancellationToken);
    }

    public virtual ValueTask<JsonRpcMethodResult> InvokeAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled<JsonRpcMethodResult>(cancellationToken);
        }

        return AsyncCore(parameters);

        async ValueTask<JsonRpcMethodResult> AsyncCore(JsonValue? parameters)
        {
            await NotifyAsync(parameters, cancellationToken);
            return JsonRpcMethodResult.Ok(JsonValues.Null);
        }
    }
}
