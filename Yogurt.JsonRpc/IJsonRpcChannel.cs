using System.Threading.Channels;

namespace Yogurt.JsonRpc;

public interface IJsonRpcChannel
{
    [PublicAPI] ChannelReader<JsonRpcMessage> Input { get; }
    [PublicAPI] ChannelWriter<JsonRpcMessage> Output { get; }
}
