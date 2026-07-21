namespace Yogurt.JsonRpc;

public interface IStartableJsonRpcChannel : IJsonRpcChannel
{
    [PublicAPI]
    Task StartAsync(CancellationToken cancellationToken = default);
}
