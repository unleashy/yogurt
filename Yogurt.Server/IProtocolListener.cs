namespace Yogurt.Server;

[PublicAPI]
public interface IProtocolListener
{
    IAsyncEnumerable<ProtocolMessage> Listen(CancellationToken cancellationToken = default);
}
