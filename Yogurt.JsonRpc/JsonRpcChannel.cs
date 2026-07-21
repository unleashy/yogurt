using System.Threading.Channels;

namespace Yogurt.JsonRpc;

public sealed class JsonRpcChannel(IJsonRpcTransport transport) : IStartableJsonRpcChannel
{
    private Channel<JsonRpcMessage> _input =
        Channel.CreateBounded<JsonRpcMessage>(
            new BoundedChannelOptions(capacity: 0) {
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = true,
                SingleReader = false,
                SingleWriter = true,
            }
        );

    private Channel<JsonRpcMessage> _output =
        Channel.CreateUnbounded<JsonRpcMessage>(
            new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false,
            }
        );

    [PublicAPI] public ChannelReader<JsonRpcMessage> Input => _input.Reader;
    [PublicAPI] public ChannelWriter<JsonRpcMessage> Output => _output.Writer;

    [PublicAPI]
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) {
            return Task.FromCanceled(cancellationToken);
        }

        return Task.WhenAll(
            SendAllAsync(cancellationToken),
            ReceiveAllAsync(cancellationToken)
        );
    }

    private async Task SendAllAsync(CancellationToken ct)
    {
        await foreach (var msg in _output.Reader.ReadAllAsync(ct)) {
            var data = msg.ToJsonText();
            await transport.WriteAsync(data, ct);
        }
    }

    private async Task ReceiveAllAsync(CancellationToken ct)
    {
        await foreach (var data in transport.ReadAllAsync(ct)) {
            var result = JsonRpcParser.Parse(data);
            await result.Match(
                message => _input.Writer.WriteAsync(message, ct),
                error => SendParseError(result.Id, error)
            );
        }

        _input.Writer.Complete();
    }

    private ValueTask SendParseError(JsonRpcId? id, JsonRpcError error)
    {
        _ = Output.TryWrite(
            JsonRpcMessage.Response(
                JsonRpcResponse.Error(id ?? JsonRpcId.Null, error)
            )
        );

        return ValueTask.CompletedTask;
    }
}
