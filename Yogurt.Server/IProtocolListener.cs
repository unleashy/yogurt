namespace Yogurt.Server;

public interface IProtocolListener
{
    IAsyncEnumerable<ProtocolMessage> Listen(CancellationToken cancellationToken = default);
}
