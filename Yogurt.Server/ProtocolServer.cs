namespace Yogurt.Server;

public sealed class ProtocolServer(
    IProtocolReader reader,
    IProtocolHandler handler,
    IProtocolWriter writer
)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var message in reader.ReadAllAsync(cancellationToken)) {
            var result = await handler.HandleAsync(message, cancellationToken);
            if (result is {} response) {
                await writer.WriteAsync(response, cancellationToken);
            }
        }
    }
}
