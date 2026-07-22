using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace Yogurt.Json;

public sealed class JsonBuilder
{
    private ArrayBufferWriter<byte> _buffer = new();
    private ImmutableArray<Token>.Builder _tokens = ImmutableArray.CreateBuilder<Token>();
    private bool _hadItem = false;
    private bool _hadMember = false;

    [PublicAPI]
    public JsonValue Build()
    {
        if (_tokens.Count == 0) {
            throw new InvalidOperationException("No tokens written");
        }

        var text = _buffer.WrittenMemory;
        _buffer = new ArrayBufferWriter<byte>();

        var tokens = _tokens.DrainToImmutable();

        return new JsonValue(text, new TokenSlice(tokens.AsMemory()));
    }

    [PublicAPI]
    public void Null()
    {
        AddTokenWithText(TokenKind.Null, "null"u8);
    }

    [PublicAPI]
    public void Boolean(bool value)
    {
        if (value) {
            AddTokenWithText(TokenKind.BoolTrue, "true"u8);
        }
        else {
            AddTokenWithText(TokenKind.BoolFalse, "false"u8);
        }
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

        var hadSpace = false;
        var length = 0;

        do {
            var span = _buffer.GetSpan(2 * length);
            hadSpace = value.TryFormat(span, out length, "G", CultureInfo.InvariantCulture);
        } while (!hadSpace);

        var offset = _buffer.WrittenCount;
        _buffer.Advance(length);
        _tokens.Add(new Token(TokenKind.Number, offset, length));
    }

    [PublicAPI]
    public void String(string value)
    {
        String(Utf8.GetBytes(value));
    }

    [PublicAPI]
    public void String(ReadOnlySpan<byte> value)
    {
        var nextIndex = value.IndexOf(NeedsEscaping);
        if (nextIndex == -1) {
            StringSimple(value);
        }
        else {
            StringComplex(value, nextIndex);
        }
    }

    private void StringSimple(ReadOnlySpan<byte> value)
    {
        Debug.Assert(value.IndexOf(NeedsEscaping) == -1);

        var offset = _buffer.WrittenCount;

        WriteText((byte)'"');
        WriteText(value);
        WriteText((byte)'"');

        AddToken(TokenKind.StringSimple, offset);
    }

    private void StringComplex(ReadOnlySpan<byte> value, int nextIndex)
    {
        Debug.Assert(0 <= nextIndex && nextIndex <= value.Length);

        AddTokenWithText(TokenKind.StringComplexStart, (byte)'"');

        while (nextIndex != -1) {
            WriteText(value[.. nextIndex]);
            var offset = _buffer.WrittenCount;
            WriteText((byte)'\\');

            var ch = value[nextIndex];
            switch ((char)ch) {
                case '"':
                case '\\': {
                    WriteText(ch);
                    AddToken(TokenKind.StringEscape, offset);
                    break;
                }

                case '\b': {
                    WriteText((byte)'b');
                    AddToken(TokenKind.StringEscape, offset);
                    break;
                }

                case '\f': {
                    WriteText((byte)'f');
                    AddToken(TokenKind.StringEscape, offset);
                    break;
                }

                case '\n': {
                    WriteText((byte)'n');
                    AddToken(TokenKind.StringEscape, offset);
                    break;
                }

                case '\r': {
                    WriteText((byte)'r');
                    AddToken(TokenKind.StringEscape, offset);
                    break;
                }

                case '\t': {
                    WriteText((byte)'t');
                    AddToken(TokenKind.StringEscape, offset);
                    break;
                }

                default: {
                    Debug.Assert(ch <= '\x1F');

                    var span = _buffer.GetSpan(5);

                    span[0] = (byte)'u';
                    _ = ch.TryFormat(span[1 ..], out _, "X4", CultureInfo.InvariantCulture);

                    _buffer.Advance(5);

                    AddToken(TokenKind.StringEscapeUnicode, offset);
                    break;
                }
            }

            value = value[(nextIndex + 1) ..];
            nextIndex = value.IndexOf(NeedsEscaping);
        }

        WriteText(value);
        AddTokenWithText(TokenKind.StringComplexEnd, (byte)'"');
    }

    private static bool NeedsEscaping(char c) => c is <= '\x1F' or '"' or '\\';

    [PublicAPI]
    public void Array(Action<JsonBuilder> action)
    {
        var prevHadItem = _hadItem;
        _hadItem = false;

        AddTokenWithText(TokenKind.ArrayOpen, (byte)'[');
        action(this);
        AddTokenWithText(TokenKind.ArrayClose, (byte)']');

        _hadItem = prevHadItem;
    }

    [PublicAPI]
    public void Item(Action<JsonBuilder> action)
    {
        if (_hadItem) {
            WriteText((byte)',');
        }

        action(this);

        _hadItem = true;
    }

    [PublicAPI]
    public void Object(Action<JsonBuilder> action)
    {
        var prevHadMember = _hadMember;
        _hadMember = false;

        AddTokenWithText(TokenKind.ObjectOpen, (byte)'{');
        action(this);
        AddTokenWithText(TokenKind.ObjectClose, (byte)'}');

        _hadMember = prevHadMember;
    }

    [PublicAPI]
    public void Member(string key, Action<JsonBuilder> action)
    {
        if (_hadMember) {
            WriteText((byte)',');
        }

        String(key);
        WriteText((byte)':');
        action(this);

        _hadMember = true;
    }

    [PublicAPI]
    public void Value(in JsonValue value)
    {
        _buffer.Write(value.Text.Span);
        _tokens.AddRange(value.Tokens.Span);
    }

    private void AddTokenWithText(TokenKind kind, ReadOnlySpan<byte> text)
    {
        var offset = _buffer.WrittenCount;
        WriteText(text);
        AddToken(kind, offset);
    }

    private void AddTokenWithText(TokenKind kind, byte ch)
    {
        var offset = _buffer.WrittenCount;
        WriteText(ch);
        AddToken(kind, offset);
    }

    private void AddToken(TokenKind kind, int offset)
    {
        _tokens.Add(new Token(kind, offset, _buffer.WrittenCount - offset));
    }

    private void WriteText(byte value)
    {
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);
    }

    private void WriteText(ReadOnlySpan<byte> value)
    {
        _buffer.Write(value);
    }
}
