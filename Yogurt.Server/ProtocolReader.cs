using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace Yogurt.Server;

public sealed class ProtocolReader(PipeReader reader) : IProtocolReader
{
    public async IAsyncEnumerable<ProtocolMessage> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        while (!cancellationToken.IsCancellationRequested) {
            var contentLength = await ReadHeaders(cancellationToken);
            if (contentLength is null) break;

            var message = await ReadContent(contentLength.Value, cancellationToken);
            if (message is null) break;

            yield return message.Value;
        }
    }

    private async ValueTask<ProtocolMessage?> ReadContent(
        int contentLength,
        CancellationToken cancellationToken = default
    )
    {
        var read = await reader.ReadAtLeastAsync(contentLength, cancellationToken);
        var buffer = read.Buffer;

        try {
            if (read.IsCanceled) {
                return null;
            }

            if (buffer.Length < contentLength) {
                throw new InvalidDataException(
                    $"Invalid content: expected {contentLength} bytes, got {buffer.Length}"
                );
            }

            var text = buffer.Slice(0, contentLength).ToArray();
            return Utf8.IsValid(text)
                ? new ProtocolMessage(text)
                : throw new InvalidDataException("Invalid content: malformed UTF-8");
        }
        finally {
            // the consumed range depends on the success or failure of the content read:
            // if success, then contentLength <= buffer.Length, thus contentLength is consumed
            // if failure, then contentLength > buffer.Length, thus buffer.Length is consumed
            reader.AdvanceTo(buffer.GetPosition(Math.Min(contentLength, buffer.Length)));
        }
    }

    private async ValueTask<int?> ReadHeaders(CancellationToken cancellationToken = default)
    {
        while (true) {
            var read = await reader.ReadAsync(cancellationToken);
            var buffer = read.Buffer;
            var seqReader = new SequenceReader<byte>(buffer);

            try {
                if (read.IsCanceled) {
                    return null;
                }

                if (seqReader.TryReadTo(out ReadOnlySequence<byte> headers, HeaderPartSep)) {
                    // re-add the final \r\n to the headers
                    headers = buffer.Slice(headers.Start, seqReader.Position);
                    return ParseHeaders(headers);
                }

                if (read.IsCompleted) {
                    return buffer.Length == 0
                        ? null
                        : throw new InvalidDataException(
                              "Invalid message: missing or malformed headers"
                          );
                }
            }
            finally {
                reader.AdvanceTo(seqReader.Position, buffer.End);
            }
        }
    }

    private static int ParseHeaders(ReadOnlySequence<byte> headers)
    {
        var reader = new SequenceReader<byte>(headers);

        int? contentLength = null;
        string? contentType = null;

        while (reader.TryReadTo(out ReadOnlySpan<byte> line, HeaderLineSep)) {
            if (line.IsEmpty) {
                break;
            }

            var header = ParseHeader(line);
            switch (header.Name.ToUpperInvariant()) {
                case "CONTENT-LENGTH": {
                    if (contentLength != null) {
                        throw new InvalidDataException(
                            "Invalid headers: duplicate Content-Length header"
                        );
                    }

                    if (
                        int.TryParse(
                            header.Value,
                            style: NumberStyles.None,
                            provider: CultureInfo.InvariantCulture,
                            out var value
                        )
                    ) {
                        contentLength = value;
                    }
                    else {
                        throw new InvalidDataException(
                            $"Invalid Content-Length: expected an integer between 0 and 2^31-1, " +
                            $"got '{header.Value}'"
                        );
                    }

                    break;
                }

                case "CONTENT-TYPE": {
                    if (contentType != null) {
                        throw new InvalidDataException(
                            "Invalid headers: duplicate Content-Type header"
                        );
                    }

                    if (
                        header.Value is
                            "application/vscode-jsonrpc; charset=utf-8" or
                            "application/vscode-jsonrpc; charset=utf8"
                    ) {
                        contentType = header.Value;
                    }
                    else {
                        throw new InvalidDataException(
                            $"Invalid Content-Type: expected " +
                            $"'application/vscode-jsonrpc; charset=utf-8', got '{header.Value}'"
                        );
                    }

                    break;
                }

                default: {
                    throw new InvalidDataException(
                        $"Invalid headers: unknown header '{header.Name}: {header.Value}'"
                    );
                }
            }
        }

        return
            contentLength ??
            throw new InvalidDataException("Invalid headers: missing Content-Length");
    }

    private static (string Name, string Value) ParseHeader(ReadOnlySpan<byte> line)
    {
        var sep = line.IndexOf(HeaderSep);
        if (sep == -1) {
            throw new InvalidDataException("Invalid header: missing ': ' separator");
        }

        return (
            Name: Encoding.ASCII.GetString(line[.. sep]).Trim(),
            Value: Encoding.ASCII.GetString(line[(sep + HeaderSep.Length) ..]).Trim()
        );
    }

    private static ReadOnlySpan<byte> HeaderSep     => ": "u8;
    private static ReadOnlySpan<byte> HeaderLineSep => "\r\n"u8;
    private static ReadOnlySpan<byte> HeaderPartSep => "\r\n\r\n"u8;
}
