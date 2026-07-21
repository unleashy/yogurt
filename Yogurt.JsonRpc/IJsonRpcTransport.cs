namespace Yogurt.JsonRpc;

public interface IJsonRpcTransport
{
    [PublicAPI]
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(
        CancellationToken cancellationToken = default
    );

    [PublicAPI]
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken = default
    );
}
