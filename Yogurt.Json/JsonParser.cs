using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Yogurt.Json;

public sealed class JsonParser
{
    private string _s;
    private bool _atArrayStart;
    private bool _atObjectStart;

    public JsonParser(string source)
    {
        _s = source;

        SkipSpaces();
        if (IsAtEnd) {
            throw new JsonException("Unexpected empty JSON document");
        }
    }

    public bool IsAtEnd => _s.Length == 0;

    public void ExpectNotAtEnd()
    {
        if (IsAtEnd) {
            throw new JsonException("Unexpected end of input");
        }
    }

    public bool Null() => SkipWithSpaces("null");

    public void ExpectNull()
    {
        if (!Null()) {
            throw new JsonException("Expected null");
        }
    }

    public bool? Boolean()
    {
        if (SkipWithSpaces("true")) {
            return true;
        }
        else if (SkipWithSpaces("false")) {
            return false;
        }
        else {
            return null;
        }
    }

    public bool ExpectBoolean()
    {
        if (Boolean() is {} value) {
            return value;
        }

        throw new JsonException("Expected a boolean");
    }

    public string? Number()
    {
        var start = _s;
        var negative = Skip('-');

        if (Skip('0')) {
            if (Has(NumberChars)) {
                throw new JsonException("Invalid leading zero in number literal");
            }
        }
        else if (!SkipWhile(NumberChars)) {
            return negative
                ? throw new JsonException("Expected a number after '-'")
                : null;
        }

        if (Skip('.')) {
            if (!SkipWhile(NumberChars)) {
                throw new JsonException("Expected digits for fractional part");
            }
        }

        if (Skip('e') || Skip('E')) {
            _ = Skip('+') || Skip('-');
            if (!SkipWhile(NumberChars)) {
                throw new JsonException("Expected digits for exponent part");
            }
        }

        var number = start[.. ^_s.Length];
        SkipSpaces();
        return number;
    }

    public T? Number<T>(
        NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent
    )
        where T : struct, INumberBase<T>
    {
        if (Number() is not {} number) return null;

        return T.TryParse(number, styles, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    public string ExpectNumber()
    {
        if (Number() is {} value) {
            return value;
        }

        throw new JsonException("Expected a number");
    }

    public T ExpectNumber<T>(
        NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent
    )
        where T : struct, INumberBase<T>
    {
        if (Number() is {} number) {
            return T.Parse(number, styles, CultureInfo.InvariantCulture);
        }

        throw new JsonException("Expected a number");
    }

    private static readonly SearchValues<char> NumberChars = SearchValues.Create(
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
    );

    public string? String()
    {
        if (Skip('"')) {
            var s = ReadString();
            SkipSpaces();
            return s;
        }
        else {
            return null;
        }
    }

    public string ExpectString()
    {
        if (String() is {} value) {
            return value;
        }

        throw new JsonException("Expected a string");
    }

    #region String handling
    private string ReadString()
    {
        var nextIndex = _s.IndexOfAny(RelevantStringChars);
        if (nextIndex == -1) {
            throw new JsonException("Unclosed string literal");
        }

        // Fast path: string has no escapes and no control characters
        return _s[nextIndex] == '"' ? AdvanceSplit(nextIndex) : ReadStringSlow(nextIndex);
    }

    private string ReadStringSlow(int nextIndex)
    {
        var buffer = new StringBuilder();

        while (nextIndex != -1) {
            var ch = _s[nextIndex];
            _ = buffer.Append(AdvanceSplit(nextIndex));

            switch (ch) {
                case '"': {
                    return buffer.ToString();
                }

                case '\\': {
                    _ = buffer.Append(InterpretEscape());
                    break;
                }

                default: {
                    Debug.Assert(ch <= '\x1F');
                    throw new JsonException("Unescaped control character in string literal");
                }
            }

            nextIndex = _s.IndexOfAny(RelevantStringChars);
        }

        throw new JsonException("Unclosed string literal");
    }

    private Rune InterpretEscape()
    {
        switch (Current) {
            case '"':  Advance(); return new Rune('\"');
            case '\\': Advance(); return new Rune('\\');
            case '/':  Advance(); return new Rune('/');
            case 'b':  Advance(); return new Rune('\b');
            case 'f':  Advance(); return new Rune('\f');
            case 'n':  Advance(); return new Rune('\n');
            case 'r':  Advance(); return new Rune('\r');
            case 't':  Advance(); return new Rune('\t');
            case 'u':  Advance(); return InterpretUnicodeEscape();
            default: {
                throw new JsonException($"Invalid escape sequence '\\{Current}'");
            }
        }
    }

    private Rune InterpretUnicodeEscape()
    {
        var highCh = ParseUnicodeEscapeHexDigits();
        if (Rune.TryCreate(highCh, out var rune)) {
            return rune;
        }

        if (char.IsLowSurrogate(highCh)) {
            throw new JsonException("Lone surrogate in string literal");
        }

        // Ensure the very next element is an unicode escape for a low surrogate
        Debug.Assert(char.IsHighSurrogate(highCh));
        if (!Skip(@"\u")) {
            throw new JsonException("Lone surrogate in string literal");
        }

        var lowCh = ParseUnicodeEscapeHexDigits();
        return Rune.TryCreate(highCh, lowCh, out rune)
            ? rune
            : throw new JsonException("Malformed surrogate pair in string literal");
    }

    private char ParseUnicodeEscapeHexDigits()
    {
        var start = _s;
        for (var i = 0; i < 4; ++i) {
            if (!char.IsAsciiHexDigit(Current)) {
                throw new JsonException(
                    "Invalid Unicode escape sequence; expected 4 hexadecimal digits"
                );
            }

            Advance();
        }

        var digits = start[.. ^_s.Length];
        Debug.Assert(digits.Length == 4);
        Debug.Assert(digits.All(char.IsAsciiHexDigit));

        // ushort.Parse cannot fail because the previous loop guarantees that the escape
        // contains exactly 4 hexadecimal digits, which can never overflow an ushort
        return (char)ushort.Parse(
            digits,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture
        );
    }

    private static readonly SearchValues<char> RelevantStringChars = SearchValues.Create(
        // Control characters
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07',
        '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17',
        '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',

        // Quote
        '"',

        // Escape
        '\\'
    );
    #endregion String handling

    public bool Array()
    {
        if (SkipWithSpaces('[')) {
            _atArrayStart = true;
            return true;
        }
        else {
            return false;
        }
    }

    public bool ArrayElement()
    {
        try {
            ExpectNotAtEnd();

            if (SkipWithSpaces(']')) {
                return false;
            }

            if (!_atArrayStart && !SkipWithSpaces(',')) {
                throw new JsonException("Expected comma ',' separating array elements");
            }

            return true;
        }
        finally {
            _atArrayStart = false;
        }
    }

    public void ExpectArray()
    {
        if (!Array()) {
            throw new JsonException("Expected an array");
        }
    }

    public IEnumerable<T> ExpectArray<T>(Func<JsonParser, T> parse)
    {
        ExpectArray();
        while (ArrayElement()) {
            yield return parse(this);
        }
    }

    public bool Object()
    {
        if (SkipWithSpaces('{')) {
            _atObjectStart = true;
            return true;
        }
        else {
            return false;
        }
    }

    public string? ObjectKey()
    {
        try {
            ExpectNotAtEnd();

            if (SkipWithSpaces('}')) {
                return null;
            }

            if (!_atObjectStart && !SkipWithSpaces(',')) {
                throw new JsonException("Expected comma ',' separating object entries");
            }

            if (!Skip('"')) {
                throw new JsonException("Expected an object key");
            }

            var key = ReadString();
            SkipSpaces();

            return SkipWithSpaces(':')
                ? key
                : throw new JsonException("Expected colon ':' separating object key from value");
        }
        finally {
            _atObjectStart = false;
        }
    }

    public void ExpectObject()
    {
        if (!Object()) {
            throw new JsonException("Expected an object");
        }
    }

    public void ExpectObject(ExpectObject.IHandler handler)
    {
        if (!Object()) {
            throw new JsonException("Expected an object");
        }

        while (ObjectKey() is {} key) {
            if (!handler.Accept(this, key)) {
                throw new JsonException($"Unexpected key '{key}'");
            }
        }

        handler.Complete();
    }

    public void ExpectObject(params ExpectObject.IHandler[] handlers) =>
        ExpectObject(Json.ExpectObject.Compose(handlers));

    public Dictionary<string, T> ExpectObjectDictionary<T>(Func<JsonParser, T> parse)
    {
        var dict = new Dictionary<string, T>();

        ExpectObject();
        while (ObjectKey() is {} key) {
            dict[key] = parse(this);
        }

        return dict;
    }

    public void SkipValue()
    {
        var isArray = new BitArray(256);
        var depth = 0;

        try {
            while (true) {
                if (Null()) {}
                else if (Boolean() is not null) {}
                else if (Number() is not null) {}
                else if (String() is not null) {}
                else if (Array()) {
                    isArray[depth++] = true;
                }
                else if (Object()) {
                    isArray[depth++] = false;
                }
                else {
                    throw new JsonException("Expected a value");
                }

                while (true) {
                    if (depth == 0) return;

                    var doBreak = isArray[depth - 1] ? ArrayElement() : ObjectKey() is not null;
                    if (doBreak) break;

                    --depth;
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new JsonException("Nesting level too deep");
        }
    }

    public string ValueAsString()
    {
        var start = _s;
        SkipValue();
        return start[.. ^_s.Length];
    }

    private bool SkipWhile(SearchValues<char> values)
    {
        var cutoff = _s.IndexOfAnyExcept(values);
        if (cutoff == -1) cutoff = _s.Length;

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

    private bool Skip(string prefix)
    {
        if (Has(prefix)) {
            AdvanceBy(prefix.Length);
            return true;
        }
        else {
            return false;
        }
    }

    private bool SkipWithSpaces(char prefix)
    {
        if (Skip(prefix)) {
            SkipSpaces();
            return true;
        }
        else {
            return false;
        }
    }

    private bool SkipWithSpaces(string prefix)
    {
        if (Skip(prefix)) {
            SkipSpaces();
            return true;
        }
        else {
            return false;
        }
    }

    private void SkipSpaces()
    {
        _s = _s.TrimStart(' ', '\n', '\r', '\t');
    }

    private bool Has(char c) => _s.StartsWith(c);

    private bool Has(string prefix) => _s.StartsWith(prefix, StringComparison.Ordinal);

    private bool Has(SearchValues<char> values) => _s.Length > 0 && values.Contains(_s[0]);

    private void Advance() => AdvanceBy(1);

    private void AdvanceBy(int n) => _s = _s[n ..];

    private string AdvanceSplit(int n)
    {
        var before = _s[.. n];
        _s = _s[(n + 1) ..];
        return before;
    }

    private char Current {
        get {
            ExpectNotAtEnd();
            return _s[0];
        }
    }
}
