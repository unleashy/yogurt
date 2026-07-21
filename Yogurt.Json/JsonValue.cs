using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Yogurt.Json;

using JsonMember = KeyValuePair<string, JsonValue>;

public readonly struct JsonValue :
    IEquatable<JsonValue>,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    internal const int MaxDepth = 64;

    private readonly ReadOnlyMemory<byte> _text;
    private readonly TokenSlice _s;

    [PublicAPI]
    public static JsonValue Parse(string text) => Parse(Utf8.GetBytes(text));

    [PublicAPI]
    public static JsonValue Parse(ReadOnlyMemory<byte> text) => new(text, Parser.Parse(text));

    internal JsonValue(ReadOnlyMemory<byte> text, TokenSlice s)
    {
        _text = text;
        _s = s;
    }

    [PublicAPI]
    public bool TryNull() => _s.Has(TokenKind.Null);

    [PublicAPI]
    public void Null()
    {
        if (!TryNull()) {
            throw KindError(TokenKind.Null);
        }
    }

    [PublicAPI]
    public bool? TryBoolean() =>
        _s.Has(TokenKind.BoolTrue) ? true
        : _s.Has(TokenKind.BoolFalse) ? false
        : null;

    [PublicAPI]
    public bool Boolean() =>
        TryBoolean() ?? throw KindError(TokenKind.BoolTrue, TokenKind.BoolFalse);

    [PublicAPI]
    public string? TryNumber() =>
        _s.Match(TokenKind.Number) is {} token ? Utf8.GetString(token.Text(_text)) : null;

    [PublicAPI]
    public string Number() =>
        TryNumber() ?? throw KindError(TokenKind.Number);

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
    public T Number<T>()
        where T : struct, INumberBase<T>
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent;

        if (TryNumber() is not {} number) throw KindError(TokenKind.Number);

        return T.TryParse(number, styles, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw Error($"Expected a number parseable as {typeof(T).Name}");
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

    [PublicAPI]
    public string String() =>
        TryString() ?? throw KindError(TokenKind.StringSimple);

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
    public bool Literal(bool literal) =>
        TryLiteral(literal) ?? throw KindError(literal ? TokenKind.BoolTrue : TokenKind.BoolFalse);

    [PublicAPI]
    public T? TryLiteral<T>(T literal)
        where T : struct, INumberBase<T>
    {
        return TryNumber<T>() is {} value && value == literal ? value : null;
    }

    [PublicAPI]
    public T Literal<T>(T literal)
        where T : struct, INumberBase<T>
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent;

        if (TryNumber() is not {} number) {
            throw KindError(string.Create(CultureInfo.InvariantCulture, $"the number {literal}"));
        }

        if (!T.TryParse(number, styles, CultureInfo.InvariantCulture, out var result)) {
            throw KindError(
                string.Create(CultureInfo.InvariantCulture, $"the number {literal}"),
                number
            );
        }

        return result;
    }

    [PublicAPI]
    public string? TryLiteral(string literal) =>
        TryString() is {} value && value == literal ? value : null;

    [PublicAPI]
    public string Literal(string literal)
    {
        if (TryString() is not {} value) {
            throw KindError($"the string {literal.JsonEscape()}");
        }

        return value == literal
            ? value
            : throw KindError($"the string {literal.JsonEscape()}", value.JsonEscape());
    }

    [PublicAPI]
    public ArrayEnumerator? TryArray()
    {
        var slice = _s.SkipIf(TokenKind.ArrayOpen, out var hadArrayOpen);
        return hadArrayOpen ? new ArrayEnumerator(_text, slice) : null;
    }

    [PublicAPI]
    public ArrayEnumerator Array() =>
        TryArray() ?? throw KindError(TokenKind.ArrayOpen);

    [PublicAPI]
    public T[]? TryArray<T>(Func<JsonValue, T?> parser)
        where T : class
    {
        return TryArray()?.Select(parser).OfType<T>().ToArray();
    }

    [PublicAPI]
    public T[]? TryArray<T>(Func<JsonValue, T?> parser)
        where T : struct
    {
        return TryArray()?.Select(parser).OfType<T>().ToArray();
    }

    [PublicAPI]
    public T[] Array<T>(Func<JsonValue, T> parser) =>
        TryArray()?.Select(parser).ToArray() ?? throw KindError(TokenKind.ArrayOpen);

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
    public ObjectEnumerator Object() => TryObject() ?? throw KindError(TokenKind.ObjectOpen);

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

    public T Object<T>(IJsonObjectReader<T> reader)
        where T : new(), allows ref struct
    {
        return Object(new T(), reader);
    }

    [PublicAPI]
    public T Object<T>(T basis, IJsonObjectReader<T> reader)
        where T : allows ref struct
    {
        return Object(reader, ref basis);
    }

    /// Private implementation of TryObject that correctly handles both struct and class types'
    /// nullability in all cases by using a ref parameter instead of a T? return
    private bool TryObject<T>(IJsonObjectReader<T> reader, scoped ref T state)
        where T : allows ref struct
    {
        if (TryObject() is not {} e) return false;

        var hadDuplicate = false;
        var foundKeys = new HashSet<string>();
        var rejectedKeys = new HashSet<string>();
        var keys = new JsonObjectReaderKeys(foundKeys, rejectedKeys);

        foreach (var (key, value) in e) {
            if (foundKeys.Add(key)) {
                if (!reader.TryRead(key, value, keys, ref state)) {
                    _ = rejectedKeys.Add(key);
                }
            }
            else {
                hadDuplicate = true;
            }
        }

        return !hadDuplicate && reader.Complete(this, keys, ref state);
    }

    private ref T Object<T>(IJsonObjectReader<T> reader, ref T state)
        where T : allows ref struct
    {
        if (TryObject() is not {} e) throw KindError(TokenKind.ObjectOpen);

        var foundKeys = new HashSet<string>();
        var keys = new JsonObjectReaderKeys(foundKeys, ImmutableHashSet<string>.Empty);

        foreach (var (key, value) in e) {
            if (foundKeys.Add(key)) {
                if (!reader.TryRead(key, value, keys, ref state)) {
                    throw Error(value, $"Could not read value for key {key.JsonEscape()}");
                }
            }
            else {
                throw Error(value, $"Unexpected duplicate key {key.JsonEscape()} in object");
            }
        }

        if (!reader.Complete(this, keys, ref state)) {
            throw Error("Could not complete read of object");
        }

        return ref state;
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

    [PublicAPI]
    public JsonValue StructuralValue() =>
        _s.First.Kind switch {
            TokenKind.ArrayOpen or TokenKind.ObjectOpen => this,
            _ => throw KindError(TokenKind.ArrayOpen, TokenKind.ObjectOpen),
        };

    [PublicAPI]
    public (string Path, int Line, int Column) GetLocationData()
    {
        var token = _s.First;
        var (line, column) = token.GetLineAndColumn(_text.Span);
        var path = JsonPath.GetPathAt(_text.Span[.. token.Offset]);

        return (path, line, column);
    }

    [PublicAPI]
    public string HumanTypeName() => _s.First.Kind.HumanName();

    [PublicAPI]
    public ReadOnlyMemory<byte> Text => _text;

    public bool Equals(JsonValue other) => _text.Equals(other._text) && _s.Equals(other._s);
    public override bool Equals(object? obj) => obj is JsonValue other && Equals(other);

    public static bool operator ==(in JsonValue left, in JsonValue right) => left.Equals(right);
    public static bool operator !=(in JsonValue left, in JsonValue right) => !left.Equals(right);

    public override int GetHashCode() => HashCode.Combine(_text, _s);

    [PublicAPI]
    public override string ToString() => $"{this}";
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider
    )
    {
        var text = _s.Text(_text);
        var ok = text.TryCopyTo(utf8Destination);
        bytesWritten = ok ? text.Length : 0;
        return ok;
    }

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider
    )
    {
        return Utf8.TryGetChars(_s.Text(_text), destination, out charsWritten);
    }

    internal ReadOnlyMemory<Token> Tokens => _s.Memory;

    private JsonValueException KindError(TokenKind expected) =>
        KindError(expected.HumanName(), _s.First.Kind.HumanName());

    private JsonValueException KindError(TokenKind expected1, TokenKind expected2) =>
        KindError(
            $"{expected1.HumanName()} or {expected2.HumanName()}",
            _s.First.Kind.HumanName()
        );

    private JsonValueException KindError(string expected) =>
        KindError(expected, _s.First.Kind.HumanName());

    private JsonValueException KindError(string expected, string actual) =>
        Error($"Expected {expected}, got {actual}");

    private JsonValueException Error(string message) => JsonValueException.Create(this, message);

    private static JsonValueException Error(JsonValue value, string message) =>
        JsonValueException.Create(value, message);
}
