namespace Yogurt.Server;

[PublicAPI]
public interface IProtocolReader
{
    [PublicAPI]
    IAsyncEnumerable<ProtocolMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}
