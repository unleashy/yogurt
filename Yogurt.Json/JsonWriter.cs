using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;

namespace Yogurt.Json;

[method: PublicAPI]
public sealed class JsonWriter(IBufferWriter<byte> writer)
{
    private bool _hadItem;
    private bool _hadMember;

    [PublicAPI]
    public void Null()
    {
        writer.Write("null"u8);
    }

    [PublicAPI]
    public void Boolean(bool value)
    {
        writer.Write(value ? "true"u8 : "false"u8);
    }

    [PublicAPI]
    public void Number<T>(T value)
        where T : INumberBase<T>
    {
        if (!(T.IsFinite(value) && T.IsRealNumber(value))) {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(value),
                actualValue: value,
                message: "Number cannot be represented in JSON"
            );
        }

        bool hadSpace;
        var bytesWritten = 0;

        do {
            var span = writer.GetSpan(2 * bytesWritten);
            hadSpace = value.TryFormat(span, out bytesWritten, "G", CultureInfo.InvariantCulture);
        } while (!hadSpace);

        writer.Advance(bytesWritten);
    }

    [PublicAPI]
    public void String(string value)
    {
        String(Utf8.GetBytes(value));
    }

    [PublicAPI]
    public void String(ReadOnlySpan<byte> value)
    {
        WriteRaw((byte)'"');

        var encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        while (true) {
            var span = writer.GetSpan();
            var status = encoder.EncodeUtf8(value, span, out var read, out var written);
            writer.Advance(written);

            if (status == OperationStatus.Done) {
                break;
            }
            else {
                Debug.Assert(status == OperationStatus.DestinationTooSmall);
                value = value[read ..];
            }
        }

        WriteRaw((byte)'"');
    }

    [PublicAPI]
    public void Array(Action<JsonWriter> action)
    {
        var prevHadItem = _hadItem;
        _hadItem = false;

        WriteRaw((byte)'[');
        action(this);
        WriteRaw((byte)']');

        _hadItem = prevHadItem;
    }

    [PublicAPI]
    public void Item(Action<JsonWriter> action)
    {
        if (_hadItem) {
            WriteRaw((byte)',');
        }

        action(this);

        _hadItem = true;
    }

    [PublicAPI]
    public void Object(Action<JsonWriter> action)
    {
        var prevHadMember = _hadMember;
        _hadMember = false;

        WriteRaw((byte)'{');
        action(this);
        WriteRaw((byte)'}');

        _hadMember = prevHadMember;
    }

    [PublicAPI]
    public void Member(string key, Action<JsonWriter> action)
    {
        if (_hadMember) {
            WriteRaw((byte)',');
        }

        String(key);
        WriteRaw((byte)':');
        action(this);

        _hadMember = true;
    }

    internal void WriteRaw(ReadOnlySpan<byte> utf8)
    {
        writer.Write(utf8);
    }

    private void WriteRaw(byte value)
    {
        writer.GetSpan(1)[0] = value;
        writer.Advance(1);
    }
}
