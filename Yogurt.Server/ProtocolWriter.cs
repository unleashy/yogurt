using System.Globalization;
using System.IO.Pipelines;

namespace Yogurt.Server;

public sealed class ProtocolWriter(PipeWriter writer)
{
    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) return;

        var header = "Content-Length: "u8;
        var separator = "\r\n\r\n"u8;
        var messageLength = message.Length;
        var numDigits = (int)(1 + float.Max(0, float.Log10(messageLength)));
        var headersLength = header.Length + numDigits + separator.Length;

        var span = writer.GetSpan(headersLength);

        header.CopyTo(span);
        _ = messageLength
            .TryFormat(span[header.Length ..], out _, provider: CultureInfo.InvariantCulture);
        separator.CopyTo(span[(header.Length + numDigits) ..]);

        writer.Advance(headersLength);

        _ = await writer.WriteAsync(message, cancellationToken);
    }
}
