namespace Yogurt.JsonRpc;

public abstract class JsonRpcMethod<TParams, TResult> : IJsonRpcMethod
    where TParams : IJsonParseable<TParams>
    where TResult : IJsonBuildable
{
    [PublicAPI]
    public abstract ValueTask<TResult> InvokeAsync(
        TParams parameters,
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    public virtual ValueTask<JsonRpcMethodResult> InvokeAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled<JsonRpcMethodResult>(cancellationToken);
        }

        TParams req;
        try {
            req = TParams.Parse(parameters ?? JsonValues.Null);
        }
        catch (JsonValueException e) {
            return ValueTask.FromResult(
                JsonRpcMethodResult.Error(
                    new JsonRpcError(JsonRpcErrorCodes.InvalidParams, e.Message)
                )
            );
        }

        return AsyncCore();

        async ValueTask<JsonRpcMethodResult> AsyncCore()
        {
            var result = await InvokeAsync(req, cancellationToken);
            return JsonRpcMethodResult.Ok(result.ToJson());
        }
    }

    public virtual ValueTask NotifyAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled(cancellationToken);
        }

        return AsyncCore(parameters);

        async ValueTask AsyncCore(JsonValue? parameters)
        {
            _ = await InvokeAsync(parameters, cancellationToken);
        }
    }
}
