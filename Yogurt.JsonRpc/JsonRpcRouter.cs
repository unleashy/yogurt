using System.Collections.Frozen;

namespace Yogurt.JsonRpc;

public sealed partial class JsonRpcRouter(FrozenDictionary<string, IJsonRpcMethod> methods)
    : IJsonRpcSubject
{
    [PublicAPI]
    public ValueTask<JsonRpcMethodResult> InvokeAsync(
        string method,
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    ) =>
        methods.TryGetValue(method, out var m)
            ? m.InvokeAsync(parameters, cancellationToken)
            : ValueTask.FromResult(
                  JsonRpcMethodResult.Error(
                      new JsonRpcError(JsonRpcErrorCodes.MethodNotFound, "Method not found")
                  )
              );

    public ValueTask NotifyAsync(
        string method,
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    ) =>
        methods.TryGetValue(method, out var m)
            ? m.NotifyAsync(parameters, cancellationToken)
            : ValueTask.CompletedTask;
}
