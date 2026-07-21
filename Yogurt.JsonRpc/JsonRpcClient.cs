using System.Collections.Concurrent;
using System.Diagnostics;

namespace Yogurt.JsonRpc;

public sealed class JsonRpcClient(IJsonRpcChannel channel) : IJsonRpcObserver
{
    private readonly record struct PendingRequest(TaskCompletionSource<JsonRpcResponse> Tcs);

    private readonly ConcurrentDictionary<JsonRpcId, PendingRequest> _pendingRequests = new();

    [PublicAPI]
    public Func<JsonRpcId> IdGenerator { get; set; } = DefaultIdGenerator.Generate;

    [PublicAPI]
    public Task<JsonRpcResponse> InvokeAsync(
        string method,
        JsonValue? parameters = null,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return Task.FromCanceled<JsonRpcResponse>(cancellationToken);
        }

        var id = IdGenerator();
        var request = new JsonRpcRequest(id, method, parameters);
        var message = JsonRpcMessage.Request(request);

        var responseTask = RegisterPending(id, cancellationToken);
        return InvokeAsyncCore(message, responseTask, cancellationToken);
    }

    private async Task<JsonRpcResponse> InvokeAsyncCore(
        JsonRpcMessage message,
        Task<JsonRpcResponse> responseTask,
        CancellationToken cancellationToken
    )
    {
        await channel.Output.WriteAsync(message, cancellationToken);
        return await responseTask;
    }

    [PublicAPI]
    public ValueTask NotifyAsync(
        string method,
        JsonValue? parameters = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = new JsonRpcRequest(id: null, method, parameters);
        var message = JsonRpcMessage.Request(request);
        return channel.Output.WriteAsync(message, cancellationToken);
    }

    [PublicAPI]
    public ValueTask<bool> OnResponseAsync(
        JsonRpcResponse response,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult(CompletePending(response));
    }

    [PublicAPI]
    public void OnComplete()
    {
        _ = channel.Output.TryComplete();
    }

    private Task<JsonRpcResponse> RegisterPending(JsonRpcId id, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<JsonRpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        if (!_pendingRequests.TryAdd(id, new PendingRequest(tcs))) {
            throw new UnreachableException(
                $"pending request duplicate ID {id}; this should never happen!"
            );
        }

        if (ct.CanBeCanceled) {
            var reg = ct.Register(() => CancelPending(id, tcs, ct));
            _ = tcs.Task.ContinueWith(_ => reg.Dispose(), CancellationToken.None);
        }

        return tcs.Task;
    }

    private bool CompletePending(JsonRpcResponse response)
    {
        if (!_pendingRequests.TryRemove(response.Id, out var r)) {
            return false;
        }

        // The only way this fails is if the TCS's Task was cancelled (via the Registration).
        // Note that we do not need to dispose the Registration because we use a continuation on
        // the TCS's Task that does so
        if (!r.Tcs.TrySetResult(response)) {
            Debug.Assert(r.Tcs.Task.IsCanceled);
        }

        return true;
    }

    private void CancelPending(
        JsonRpcId id,
        TaskCompletionSource<JsonRpcResponse> tcs,
        CancellationToken ct
    )
    {
        _ = _pendingRequests.TryRemove(id, out _);
        _ = tcs.TrySetCanceled(ct);
    }

    ValueTask<bool> IJsonRpcObserver.OnRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(false);
    }
}

file static class DefaultIdGenerator
{
    [ThreadStatic] private static Random? _random;

    public static JsonRpcId Generate()
    {
        var random = _random ??= new Random();

        var s = random.GetString("0123456789abcdefghijklmnopqrstuvwxyz", length: 12);

        return JsonRpcId.String(s);
    }
}
