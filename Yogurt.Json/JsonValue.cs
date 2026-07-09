using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Yogurt.Json;

public sealed class JsonValue
{
    internal const int MaxDepth = 128;

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    private TokenSlice _s;

    [PublicAPI]
    public static JsonValue Parse(string text) => Parse(Utf8.GetBytes(text));

    [PublicAPI]
    public static JsonValue Parse(ReadOnlyMemory<byte> text) => new(Parser.Parse(text));

    private JsonValue(TokenSlice s)
    {
        _s = s;
    }

    [PublicAPI]
    public bool TryNull()
    {
        _s = _s.SkipIf(TokenKind.Null, out var hadNull);
        return hadNull;
    }

    [PublicAPI]
    public bool? TryBoolean()
    {
        _s = _s.SkipIf(TokenKind.BoolTrue, out var hadTrue, TokenKind.BoolFalse, out var hadFalse);
        return
            hadTrue ? true
            : hadFalse ? false
            : null;
    }

    [PublicAPI]
    public string? TryNumber()
    {
        (_s, var result) = _s.SkipIf(TokenKind.Number);
        return result is {} token ? TokenTextAsString(token) : null;
    }

    [PublicAPI]
    public T? TryNumber<T>()
        where T : struct, INumberBase<T>
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent;

        if (TryNumber() is not {} number) return null;

        return T.TryParse(number, styles, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    [PublicAPI]
    public string? TryString()
    {
        switch (_s.First) {
            case { Kind: TokenKind.StringSimple } token: {
                _s = _s.Skip();
                return ReadSimpleString(token);
            }

            case { Kind: TokenKind.StringComplexStart } token: {
                _s = _s.Skip();
                return ReadComplexString(token);
            }

            default: return null;
        }
    }

    #region String handling
    private string ReadSimpleString(Token token)
    {
        Debug.Assert(token.Kind == TokenKind.StringSimple);

        // Get the string by removing the quotes. There are no escapes to handle.
        return Utf8.GetString(TokenText(token)[1 .. ^1]);
    }

    private string ReadComplexString(Token token)
    {
        Debug.Assert(token.Kind == TokenKind.StringComplexStart);

        var start = token.Offset + 1;
        (_s, var end) = _s.FindSplit(TokenKind.StringComplexEnd, out var escapes);

        var buffer = new StringBuilder();

        foreach (var escape in escapes) {
            var prev = _s.Text.Span[start .. escape.Offset];
            start = escape.Offset + escape.Length;

            _ = buffer
                .Append(Utf8.GetString(prev))
                .Append(InterpretEscape(escape.Kind, TokenText(escape)));
        }

        var remainder = _s.Text.Span[start .. end.Offset];
        _ = buffer.Append(Utf8.GetString(remainder));

        return buffer.ToString();
    }

    private static Rune InterpretEscape(TokenKind kind, ReadOnlySpan<byte> escape)
    {
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (kind) {
            case TokenKind.StringEscape: {
                Debug.Assert(escape.Length == 2);
                Debug.Assert(escape[0] == '\\');

                return (char)escape[1] switch {
                    '"'  => new Rune('\"'),
                    '\\' => new Rune('\\'),
                    '/'  => new Rune('/'),
                    'b'  => new Rune('\b'),
                    'f'  => new Rune('\f'),
                    'n'  => new Rune('\n'),
                    'r'  => new Rune('\r'),
                    't'  => new Rune('\t'),
                    _    => throw new UnreachableException(),
                };
            }

            case TokenKind.StringEscapeUnicode: {
                Debug.Assert(escape.Length == 6);
                Debug.Assert(escape[0] == '\\');
                Debug.Assert(escape[1] == 'u');

                return new Rune(ParseUnicodeEscapeHexDigits(escape[2 ..]));
            }

            case TokenKind.StringEscapeUnicodePair: {
                Debug.Assert(escape.Length == 12);
                Debug.Assert(escape[0] == '\\');
                Debug.Assert(escape[1] == 'u');
                Debug.Assert(escape[6] == '\\');
                Debug.Assert(escape[7] == 'u');

                var highCh = ParseUnicodeEscapeHexDigits(escape[2 .. 6]);
                var lowCh = ParseUnicodeEscapeHexDigits(escape[8 ..]);

                var ok = Rune.TryCreate(highCh, lowCh, out var rune);
                Debug.Assert(ok);
                return rune;
            }

            default: {
                throw new UnreachableException();
            }
        }
    }

    private static char ParseUnicodeEscapeHexDigits(ReadOnlySpan<byte> s)
    {
        var ok = ushort.TryParse(
            s[.. 4],
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out var result
        );

        Debug.Assert(ok);
        return (char)result;
    }
    #endregion String handling

    [PublicAPI]
    public bool TryArray()
    {
        _s = _s.SkipIf(TokenKind.ArrayOpen, out var hadArrayOpen);
        return hadArrayOpen;
    }

    [PublicAPI]
    public bool TryArrayElement()
    {
        _s = _s.SkipIf(TokenKind.ArrayClose, out var hadArrayClose);
        return !hadArrayClose;
    }

    [PublicAPI]
    public bool TryObject()
    {
        _s = _s.SkipIf(TokenKind.ObjectOpen, out var hadObjectOpen);
        return hadObjectOpen;
    }

    [PublicAPI]
    public string? TryObjectKey()
    {
        _s = _s.SkipIf(TokenKind.ObjectClose, out var hadObjectClose);
        if (hadObjectClose) return null;

        var key = TryString();
        Debug.Assert(key is not null);
        return key;
    }

    private ReadOnlySpan<byte> TokenText(Token token) =>
        _s.Text.Span.Slice(token.Offset, token.Length);

    private string TokenTextAsString(Token token) => Utf8.GetString(TokenText(token));
}
