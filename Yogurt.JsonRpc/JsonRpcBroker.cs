namespace Yogurt.JsonRpc;

public sealed class JsonRpcBroker(IStartableJsonRpcChannel channel, IJsonRpcObserver observer)
{
    [PublicAPI]
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) {
            return Task.FromCanceled(cancellationToken);
        }

        var channelTask = channel.StartAsync(cancellationToken);
        var brokerTask = StartAsyncCore(cancellationToken);

        return Task.WhenAll(channelTask, brokerTask);
    }

    [PublicAPI]
    private async Task StartAsyncCore(CancellationToken cancellationToken = default)
    {
        await foreach (var message in channel.Input.ReadAllAsync(cancellationToken)) {
            _ = await message.Match(
                request => observer.OnRequestAsync(request, cancellationToken),
                response => observer.OnResponseAsync(response, cancellationToken)
            );
        }

        observer.OnComplete();
    }
}
