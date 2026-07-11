namespace Yogurt.Server;

public interface IProtocolWriter
{
    [PublicAPI]
    ValueTask WriteAsync(ProtocolMessage message, CancellationToken cancellationToken = default);
}
