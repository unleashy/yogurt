namespace Yogurt.Server;

public interface IProtocolHandler
{
    [PublicAPI]
    ValueTask<ProtocolMessage?> HandleAsync(
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    );
}
