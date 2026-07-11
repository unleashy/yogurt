using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Yogurt.Json;

internal ref struct Parser
{
    public static TokenSlice Parse(ReadOnlyMemory<byte> text)
    {
        var p = new Parser(text.Span);
        p.Parse();
        return new TokenSlice(p.Tokens());
    }

    private readonly ReadOnlySpan<byte> _text;
    private ReadOnlySpan<byte> _s;
    private ImmutableArray<Token>.Builder _tokens = ImmutableArray.CreateBuilder<Token>();
    private BitArray _nesting = new(length: JsonValue.MaxDepth);
    private int _depth = 0;
    private bool _atStructureStart = false;

    private Parser(ReadOnlySpan<byte> text)
    {
        _text = text;
        _s = text;
    }

    private ReadOnlyMemory<Token> Tokens() => ImmutableCollectionsMarshal.AsMemory(_tokens);

    private void Parse()
    {
        while (true) {
            SkipSpaces();
            var start = _s;

            if (IsEmpty) break;
            var c = Current;

            switch (c) {
                case '{': {
                    _tokens.Add(ObjectOpen(start));
                    break;
                }

                case '[': {
                    _tokens.Add(ArrayOpen(start));
                    break;
                }

                case '"': {
                    _tokens.Add(String(start));
                    break;
                }

                case >= '0' and <= '9' or '-' or '.': {
                    _tokens.Add(Number(start));
                    break;
                }

                case 'n' when Skip("null"u8): {
                    _tokens.Add(Token(TokenKind.Null, start));
                    break;
                }

                case 't' when Skip("true"u8): {
                    _tokens.Add(Token(TokenKind.BoolTrue, start));
                    break;
                }

                case 'f' when Skip("false"u8): {
                    _tokens.Add(Token(TokenKind.BoolFalse, start));
                    break;
                }

                default: {
                    throw ErrorUnexpected(c);
                }
            }

            while (true) {
                if (_depth == 0) goto end;

                if (_nesting[_depth - 1]) {
                    if (ArrayCommaOrClose()) {
                        break;
                    }
                }
                else {
                    if (ObjectCommaOrClose()) {
                        break;
                    }
                }

                --_depth;
            }
        }

    end:
        SkipSpaces();
        if (!IsEmpty) {
            throw ErrorTrailingData();
        }

        if (_tokens.Count == 0) {
            throw ErrorEmpty();
        }
    }

    private Token ObjectOpen(ReadOnlySpan<byte> start)
    {
        if (_depth > _nesting.Length) {
            throw ErrorTooDeep();
        }

        _nesting[_depth++] = false;
        _atStructureStart = true;

        Advance();
        return Token(TokenKind.ObjectOpen, start);
    }

    private Token ArrayOpen(ReadOnlySpan<byte> start)
    {
        if (_depth > _nesting.Length) {
            throw ErrorTooDeep();
        }

        _nesting[_depth++] = true;
        _atStructureStart = true;

        Advance();
        return Token(TokenKind.ArrayOpen, start);
    }

    private bool ArrayCommaOrClose()
    {
        SkipSpaces();
        if (IsEmpty) throw ErrorUnclosedArray();

        try {
            var start = _s;

            switch (Current) {
                case ']': {
                    Advance();
                    _tokens.Add(Token(TokenKind.ArrayClose, start));
                    return false;
                }

                case ',' when !_atStructureStart: {
                    Advance();

                    SkipSpaces();
                    if (Has(']')) {
                        throw ErrorTrailingComma();
                    }

                    return true;
                }

                default: {
                    if (!_atStructureStart) {
                        throw ErrorMissingArrayComma(Current);
                    }

                    return true;
                }
            }
        }
        finally {
            _atStructureStart = false;
        }
    }

    private bool ObjectCommaOrClose()
    {
        SkipSpaces();
        if (IsEmpty) throw ErrorUnclosedObject();

        try {
            var start = _s;

            switch (Current) {
                case '}': {
                    Advance();
                    _tokens.Add(Token(TokenKind.ObjectClose, start));
                    return false;
                }

                case ',' when !_atStructureStart: {
                    Advance();

                    SkipSpaces();
                    if (Has('}')) {
                        throw ErrorTrailingComma();
                    }

                    break;
                }

                default: {
                    if (!_atStructureStart) {
                        throw ErrorMissingObjectComma(Current);
                    }

                    break;
                }
            }

            SkipSpaces();
            start = _s;

            if (!Has('"')) {
                if (IsEmpty) {
                    throw ErrorUnclosedObject();
                }
                else if (Has(',')) {
                    throw ErrorUnexpected(',');
                }
                else {
                    throw ErrorInvalidObjectKey(Current);
                }
            }

            _tokens.Add(String(start));

            SkipSpaces();
            if (!Skip(':')) {
                if (IsEmpty) {
                    throw ErrorUnclosedObject();
                }
                else {
                    throw ErrorMissingObjectColon(Current);
                }
            }

            return true;
        }
        finally {
            _atStructureStart = false;
        }
    }

    private Token String(ReadOnlySpan<byte> start)
    {
        var quote = Skip('"');
        Debug.Assert(quote);

        var nextIndex = _s.IndexOf(IsRelevantStringChar);
        if (nextIndex == -1) {
            throw ErrorUnterminatedString(start);
        }

        if (_s[nextIndex] == '"') {
            AdvanceBy(nextIndex + 1);
            return Token(TokenKind.StringSimple, start);
        }
        else {
            return StringComplex(nextIndex, start);
        }
    }

    #region String handling
    private Token StringComplex(int nextIndex, ReadOnlySpan<byte> start)
    {
        _tokens.Add(Token(TokenKind.StringComplexStart, start));

        while (nextIndex != -1) {
            var ch = (char)_s[nextIndex];
            start = _s[nextIndex ..];
            _s = _s[(nextIndex + 1) ..];

            switch (ch) {
                case '\\': {
                    _tokens.Add(InterpretEscape(start));
                    break;
                }

                case '"': {
                    return Token(TokenKind.StringComplexEnd, start);
                }

                default: {
                    Debug.Assert(ch <= '\x1F');
                    throw ErrorControlChar(ch, start);
                }
            }

            nextIndex = _s.IndexOf(IsRelevantStringChar);
        }

        throw ErrorUnterminatedString(start);
    }

    private Token InterpretEscape(ReadOnlySpan<byte> start)
    {
        if (IsEmpty) {
            throw ErrorUnterminatedEscape();
        }

        switch (Current) {
            case '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't': {
                Advance();
                return Token(TokenKind.StringEscape, start);
            }

            case 'u': {
                Advance();
                return InterpretUnicodeEscape(start);
            }

            default: {
                throw ErrorInvalidEscape(Current);
            }
        }
    }

    private Token InterpretUnicodeEscape(ReadOnlySpan<byte> start)
    {
        var highCh = ParseUnicodeEscapeHexDigits();
        if (Rune.IsValid(highCh)) {
            return Token(TokenKind.StringEscapeUnicode, start);
        }

        if (char.IsLowSurrogate(highCh)) {
            throw ErrorLoneLowSurrogateEscape(highCh);
        }

        // Ensure the very next element is an unicode escape for a low surrogate
        Debug.Assert(char.IsHighSurrogate(highCh));
        if (!Skip(@"\u"u8)) {
            throw ErrorLoneHighSurrogateEscape(highCh);
        }

        var lowCh = ParseUnicodeEscapeHexDigits();
        return char.IsSurrogatePair(highCh, lowCh)
            ? Token(TokenKind.StringEscapeUnicodePair, start)
            : throw ErrorBadSurrogatePair(highCh, lowCh);
    }

    private char ParseUnicodeEscapeHexDigits()
    {
        var start = _s;
        for (var i = 0; i < 4; ++i) {
            if (IsEmpty) {
                throw ErrorUnterminatedEscape();
            }

            if (!char.IsAsciiHexDigit(Current)) {
                throw ErrorShortUnicodeEscape(i);
            }

            Advance();
        }

        var digits = start[.. ^_s.Length];
        Debug.Assert(digits.Length == 4);
        Debug.Assert(digits.ToArray().All(it => char.IsAsciiHexDigit((char)it)));

        // ushort.Parse cannot fail because the previous loop guarantees that the escape
        // contains exactly 4 hexadecimal digits, which can never overflow an ushort
        return (char)ushort.Parse(
            digits,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture
        );
    }

    private static bool IsRelevantStringChar(char c) => c is <= '\x1F' or '"' or '\\';
    #endregion String handling

    private Token Number(ReadOnlySpan<byte> start)
    {
        if (Skip('.')) {
            throw ErrorLeadingDecPoint();
        }

        var negative = Skip('-');

        if (Skip('0')) {
            if (Has(char.IsAsciiDigit)) {
                throw ErrorLeadingZero();
            }
        }
        else if (!SkipWhile(char.IsAsciiDigit) && negative) {
            throw ErrorMissingNegDigit();
        }

        if (Skip('.')) {
            if (!SkipWhile(char.IsAsciiDigit)) {
                throw ErrorMissingFracDigit();
            }
        }

        if (Skip('e') || Skip('E')) {
            _ = Skip('+') || Skip('-');
            if (!SkipWhile(char.IsAsciiDigit)) {
                throw ErrorMissingExpDigit();
            }
        }

        return Token(TokenKind.Number, start);
    }

    private Token Token(TokenKind kind, ReadOnlySpan<byte> start)
    {
        var offset = _text.Length - start.Length;
        var length = start.Length - _s.Length;
        return new Token(kind, offset, length);
    }

    private void SkipSpaces()
    {
        _s = _s.TrimStart(" \n\r\t"u8);
    }

    private bool SkipWhile(Predicate<char> predicate)
    {
        var cutoff = 0;
        while (cutoff < _s.Length) {
            if (predicate((char)_s[cutoff])) {
                ++cutoff;
            }
            else {
                break;
            }
        }

        _s = _s[cutoff ..];
        return cutoff != 0;
    }

    private bool Skip(char prefix)
    {
        if (Has(prefix)) {
            Advance();
            return true;
        }
        else {
            return false;
        }
    }

    private bool Skip(ReadOnlySpan<byte> prefix)
    {
        if (Has(prefix)) {
            AdvanceBy(prefix.Length);
            return true;
        }
        else {
            return false;
        }
    }

    private bool Has(char c) => _s.StartsWith((byte)c);

    private bool Has(ReadOnlySpan<byte> prefix) => _s.StartsWith(prefix);

    private bool Has(Predicate<char> predicate) => !IsEmpty && predicate(Current);

    private void Advance()
    {
        AdvanceBy(1);
    }

    private void AdvanceBy(int n)
    {
        Debug.Assert(_s.Length >= n);
        _s = _s[n ..];
    }

    private bool IsEmpty => _s.IsEmpty;

    private char Current => (char)_s[0];

    private JsonParseException ErrorEmpty() =>
        Error("Unexpected end of input: no JSON value found");

    private JsonParseException ErrorUnexpected(char c) =>
        Error($"Unexpected character '{c}'");

    private JsonParseException ErrorTrailingData() =>
        Error($"Unexpected trailing content after JSON value: '{Encoding.UTF8.GetString(_s)}'");

    private JsonParseException ErrorTooDeep() =>
        Error($"Maximum nesting depth of {JsonValue.MaxDepth} exceeded");

    private JsonParseException ErrorUnclosedObject() =>
        Error("Unexpected end of input: unclosed object");

    private JsonParseException ErrorMissingObjectComma(char c) =>
        Error($"Expected ',' or '}}' after value in object; found '{c}'");

    private JsonParseException ErrorInvalidObjectKey(char c) =>
        Error($"Object keys must be strings; found '{c}'");

    private JsonParseException ErrorMissingObjectColon(char c) =>
        Error($"Expected ':' after object key; found '{c}'");

    private JsonParseException ErrorUnclosedArray() =>
        Error("Unexpected end of input: unclosed array");

    private JsonParseException ErrorTrailingComma() =>
        Error("Trailing comma is not allowed");

    private JsonParseException ErrorMissingArrayComma(char c) =>
        Error($"Expected ',' or ']' after value in array; found '{c}'");

    private JsonParseException ErrorLeadingZero() =>
        Error("Invalid number: leading zero is not allowed");

    private JsonParseException ErrorLeadingDecPoint() =>
        Error("Invalid number: leading decimal point is not allowed");

    private JsonParseException ErrorMissingNegDigit() =>
        Error("Invalid number: expected digit after negative sign '-'");

    private JsonParseException ErrorMissingFracDigit() =>
        Error("Invalid number: expected digit after decimal point '.'");

    private JsonParseException ErrorMissingExpDigit() =>
        Error("Invalid number: expected digit after exponent");

    private JsonParseException ErrorUnterminatedString(ReadOnlySpan<byte> start) =>
        Error("Unterminated string", start);

    private JsonParseException ErrorControlChar(char c, ReadOnlySpan<byte> start) =>
        Error($@"Unescaped control character '\x{(ushort)c:X2}' in string", start);

    private JsonParseException ErrorUnterminatedEscape() =>
        Error("Unexpected end of input while parsing escape sequence");

    private JsonParseException ErrorInvalidEscape(char c) =>
        Error($@"Invalid escape sequence '\{c}' in string");

    private JsonParseException ErrorShortUnicodeEscape(int n) =>
        Error($"Invalid Unicode escape sequence: expected 4 hexadecimal digits; found {n}");

    private JsonParseException ErrorLoneLowSurrogateEscape(char c) =>
        Error(
            $@"Invalid Unicode escape sequence: unexpected low surrogate '\u{(ushort)c:X4}' not " +
            $"following a high surrogate"
        );

    private JsonParseException ErrorLoneHighSurrogateEscape(char c) =>
        Error(
            $@"Invalid Unicode escape sequence: expected high surrogate '\u{(ushort)c:X4}' to " +
            $"be paired with a low surrogate"
        );

    private JsonParseException ErrorBadSurrogatePair(char highCh, char lowCh) =>
        Error(
            $"Invalid Unicode escape sequence: expected high surrogate " +
            $@"'\u{(ushort)highCh:X4}' to be followed by a low surrogate; " +
            $@"found '\u{(ushort)lowCh:X4}'"
        );

    private JsonParseException Error(string message) => Error(message, _s);

    private JsonParseException Error(string message, ReadOnlySpan<byte> start)
    {
        var (line, column) = GetLineAndColumn(_text, start);
        return new JsonParseException(message, line, column);
    }

    private static (int, int) GetLineAndColumn(ReadOnlySpan<byte> start, ReadOnlySpan<byte> end)
    {
        var line = 0;
        var column = 0;

        var offset = start.Length - end.Length;
        for (var i = 1; i <= offset; ++i) {
            ++column;

            if (start[i - 1] == '\n') {
                ++line;
                column = 0;
            }
        }

        return (line, column);
    }
}

file static class SpanExtensions
{
    public static int IndexOf(this ReadOnlySpan<byte> span, Func<char, bool> predicate)
    {
        for (var i = 0; i < span.Length; ++i) {
            if (predicate((char)span[i])) {
                return i;
            }
        }

        return -1;
    }
}
