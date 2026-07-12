using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Yogurt.Json;

public readonly struct JsonValue
{
    internal const int MaxDepth = 64;

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    private readonly ReadOnlyMemory<byte> _text;
    private readonly TokenSlice _s;

    [PublicAPI]
    public static JsonValue Parse(string text) => Parse(Utf8.GetBytes(text));

    [PublicAPI]
    public static JsonValue Parse(ReadOnlyMemory<byte> text) => new(text, Parser.Parse(text));

    private JsonValue(ReadOnlyMemory<byte> text, TokenSlice s)
    {
        _text = text;
        _s = s;
    }

    [PublicAPI]
    public bool TryNull() => _s.Has(TokenKind.Null);

    [PublicAPI]
    public bool? TryBoolean() =>
        _s.Has(TokenKind.BoolTrue) ? true
        : _s.Has(TokenKind.BoolFalse) ? false
        : null;

    [PublicAPI]
    public string? TryNumber() =>
        _s.Match(TokenKind.Number) is {} token ? Utf8.GetString(token.Text(_text)) : null;

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
        return _s.First switch {
            { Kind: TokenKind.StringSimple } token =>
                ReadSimpleString(_text, token),

            { Kind: TokenKind.StringComplexStart } token =>
                ReadComplexString(_text, _s.Skip(), token),

            _ => null,
        };
    }

    #region String handling
    private static string ReadSimpleString(ReadOnlyMemory<byte> text, Token token)
    {
        Debug.Assert(token.Kind == TokenKind.StringSimple);

        // Get the string by removing the quotes. There are no escapes to handle.
        return Utf8.GetString(token.Text(text)[1 .. ^1]);
    }

    private static string ReadComplexString(
        ReadOnlyMemory<byte> text,
        TokenSlice slice,
        Token token
    )
    {
        Debug.Assert(token.Kind == TokenKind.StringComplexStart);

        var start = token.Offset + 1;
        var end = slice.FindSplit(TokenKind.StringComplexEnd, out var escapes);

        var buffer = new StringBuilder();

        foreach (var escape in escapes.Span) {
            var prev = text.Span[start .. escape.Offset];
            start = escape.Offset + escape.Length;

            _ = buffer
                .Append(Utf8.GetString(prev))
                .Append(InterpretEscape(escape.Kind, escape.Text(text)));
        }

        var remainder = text.Span[start .. end.Offset];
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
    public bool? TryLiteral(bool literal) =>
        TryBoolean() is {} value && value == literal ? value : null;

    [PublicAPI]
    public T? TryLiteral<T>(T literal)
        where T : struct, INumberBase<T>
    {
        return TryNumber<T>() is {} value && value == literal ? value : null;
    }

    [PublicAPI]
    public string? TryLiteral(string literal) =>
        TryString() is {} value && value == literal ? value : null;

    [PublicAPI]
    public ArrayEnumerator? TryArray()
    {
        var slice = _s.SkipIf(TokenKind.ArrayOpen, out var hadArrayOpen);
        return hadArrayOpen ? new ArrayEnumerator(_text, slice) : null;
    }

    [PublicAPI]
    public T[]? TryArray<T>(Func<JsonValue, T?> parser)
        where T : struct
    {
        return TryArray()?.Select(parser).OfType<T>().ToArray();
    }

    public struct ArrayEnumerator : IEnumerator<JsonValue>, IEnumerable<JsonValue>
    {
        private readonly ReadOnlyMemory<byte> _text;
        private TokenSlice _s;
        private JsonValue? _current;

        internal ArrayEnumerator(ReadOnlyMemory<byte> text, TokenSlice s)
        {
            _text = text;
            _s = s;
            _current = null;
        }

        [PublicAPI]
        public bool MoveNext()
        {
            if (_s.Has(TokenKind.ArrayClose)) {
                _current = null;
                return false;
            }

            (var token, _s) = _s.SkipValue();
            _current = new JsonValue(_text, token);
            return true;
        }

        [PublicAPI]
        public JsonValue Current => _current ?? throw new InvalidOperationException();

        [PublicAPI]
        public ArrayEnumerator GetEnumerator() => new(_text, _s);

        object IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }

        void IDisposable.Dispose() {}

        IEnumerator<JsonValue> IEnumerable<JsonValue>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [PublicAPI]
    public ObjectEnumerator? TryObject()
    {
        var slice = _s.SkipIf(TokenKind.ObjectOpen, out var hadObjectOpen);
        return hadObjectOpen ? new ObjectEnumerator(_text, slice) : null;
    }

    [PublicAPI]
    public T? TryObject<T>(T? basis, IJsonObjectReader<T> reader)
        where T : class
    {
        return basis is {} b && TryObject(reader, ref b) ? b : null;
    }

    [PublicAPI]
    public T? TryObject<T>(T? basis, IJsonObjectReader<T> reader)
        where T : struct
    {
        return basis is {} b && TryObject(reader, ref b) ? b : null;
    }

    /// Private implementation of TryObject that correctly handles both struct and class types'
    /// nullability in all cases by using a ref parameter instead of a T? return
    private bool TryObject<T>(IJsonObjectReader<T> reader, scoped ref T state)
    {
        if (TryObject() is not {} e) return false;

        var foundKeys = new HashSet<string>();
        var rejectedKeys = new HashSet<string>();
        var hadDuplicate = false;

        foreach (var (key, value) in e) {
            if (foundKeys.Add(key)) {
                if (!reader.TryRead(key, value, ref state)) {
                    _ = rejectedKeys.Add(key);
                }
            }
            else {
                hadDuplicate = true;
            }
        }

        return !hadDuplicate && reader.Complete(foundKeys, rejectedKeys, ref state);
    }

    public struct ObjectEnumerator : IEnumerator<JsonMember>, IEnumerable<JsonMember>
    {
        private readonly ReadOnlyMemory<byte> _text;
        private TokenSlice _s;
        private JsonMember? _current;

        internal ObjectEnumerator(ReadOnlyMemory<byte> text, TokenSlice s)
        {
            _text = text;
            _s = s;
            _current = null;
        }

        [PublicAPI]
        public bool MoveNext()
        {
            if (_s.Has(TokenKind.ObjectClose)) {
                _current = null;
                return false;
            }

            if (!_s.Has(TokenKind.StringSimple) && !_s.Has(TokenKind.StringComplexStart)) {
                _current = null;
                throw new InvalidOperationException();
            }

            (var keyToken, _s) = _s.SkipValue();
            (var valueToken, _s) = _s.SkipValue();

            var key = new JsonValue(_text, keyToken).TryString();
            Debug.Assert(key is not null);
            var value = new JsonValue(_text, valueToken);

            _current = new JsonMember(key, value);
            return true;
        }

        [PublicAPI]
        public JsonMember Current => _current ?? throw new InvalidOperationException();

        [PublicAPI]
        public ObjectEnumerator GetEnumerator() => new(_text, _s);

        object IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }

        void IDisposable.Dispose() {}

        IEnumerator<JsonMember> IEnumerable<JsonMember>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [PublicAPI]
    public JsonValue? TryStructuralValue() =>
        _s.First.Kind switch {
            TokenKind.ArrayOpen or TokenKind.ObjectOpen => this,
            _ => null,
        };
}
