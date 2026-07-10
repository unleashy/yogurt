namespace Yogurt.Server;

[PublicAPI]
public interface IProtocolReader
{
    IAsyncEnumerable<ProtocolMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}
