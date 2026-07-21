using System.Threading.Channels;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

internal sealed class FakeChannel : IStartableJsonRpcChannel
{
    private readonly Channel<JsonRpcMessage> _input =
        Channel.CreateUnbounded<JsonRpcMessage>(
            new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
            }
        );

    private readonly Channel<JsonRpcMessage> _output =
        Channel.CreateUnbounded<JsonRpcMessage>(
            new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
            }
        );

    public ChannelReader<JsonRpcMessage> Input => _input.Reader;
    public ChannelWriter<JsonRpcMessage> Output => _output.Writer;

    public ChannelWriter<JsonRpcMessage> InputWriter => _input.Writer;
    public ChannelReader<JsonRpcMessage> OutputReader => _output.Reader;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Complete()
    {
        _ = Output.TryComplete();
        _ = InputWriter.TryComplete();
    }
}
