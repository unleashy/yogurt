namespace Yogurt.Server;

public readonly struct ProtocolMessage(ReadOnlyMemory<byte> utf8Text)
{
    public ReadOnlyMemory<byte> Utf8Text { get; } = utf8Text;
}
