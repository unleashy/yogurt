using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Yogurt.JsonRpc;

public sealed class JsonRpcClient(IJsonRpcChannel channel) : IJsonRpcObserver
{
    private readonly record struct PendingRequest(
        JsonRpcId Id,
        TaskCompletionSource<JsonRpcResponse> Tcs
    );

    private readonly ConcurrentDictionary<JsonRpcId, PendingRequest> _pendingRequests = new();

    [PublicAPI]
    public Func<JsonRpcId> IdGenerator { get; set; } = DefaultIdGenerator.Generate;

    [PublicAPI]
    public Task<JsonRpcResponse> InvokeAsync<T>(
        string method,
        T parameters,
        CancellationToken cancellationToken = default
    )
        where T : IJsonBuildable, allows ref struct
    {
        return InvokeAsync(method, parameters.ToJson(), cancellationToken);
    }

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

        var pending = RegisterPending(id, cancellationToken);
        return InvokeAsyncCore(message, pending, cancellationToken);
    }

    private async Task<JsonRpcResponse> InvokeAsyncCore(
        JsonRpcMessage message,
        PendingRequest pending,
        CancellationToken cancellationToken
    )
    {
        try {
            await channel.Output.WriteAsync(message, cancellationToken);
        }
        catch (ChannelClosedException e) {
            FaultPending(pending, e);
        }

        return await pending.Tcs.Task;
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
        var e = ExceptionDispatchInfo.SetCurrentStackTrace(new ChannelClosedException());

        while (!_pendingRequests.IsEmpty) {
            foreach (var (id, pending) in _pendingRequests) {
                _ = _pendingRequests.TryRemove(id, out _);
                _ = pending.Tcs.TrySetException(e);
            }
        }
    }

    private PendingRequest RegisterPending(JsonRpcId id, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<JsonRpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var pending = new PendingRequest(id, tcs);
        if (!_pendingRequests.TryAdd(id, pending)) {
            throw new UnreachableException(
                $"pending request duplicate ID {id}; this should never happen!"
            );
        }

        if (ct.CanBeCanceled) {
            var reg = ct.Register(() => CancelPending(pending, ct));
            _ = tcs.Task.ContinueWith(_ => reg.Dispose(), CancellationToken.None);
        }

        return pending;
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

    private void CancelPending(PendingRequest pending, CancellationToken ct)
    {
        _ = _pendingRequests.TryRemove(pending.Id, out _);
        _ = pending.Tcs.TrySetCanceled(ct);
    }

    private void FaultPending(PendingRequest pending, Exception e)
    {
        _ = _pendingRequests.TryRemove(pending.Id, out _);
        _ = pending.Tcs.TrySetException(e);
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
