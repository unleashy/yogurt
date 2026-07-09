namespace Yogurt.Server;

[method: PublicAPI]
public readonly struct ProtocolMessage(ReadOnlyMemory<byte> utf8Text)
{
    [PublicAPI]
    public ReadOnlyMemory<byte> Utf8Text { get; } = utf8Text;
}
