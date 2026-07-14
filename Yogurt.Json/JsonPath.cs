using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Yogurt.Json;

public ref struct JsonPath
{
    private readonly record struct Part(int? Index = null, string? Property = null);

    private const byte Quote = (byte)'\"';
    private const byte Escape = (byte)'\\';
    private const byte Comma = (byte)',';
    private const byte Colon = (byte)':';
    private const byte ArrayOpen = (byte)'[';
    private const byte ArrayClose = (byte)']';
    private const byte ObjectOpen = (byte)'{';
    private const byte ObjectClose = (byte)'}';

    private ReadOnlySpan<byte> _json;
    private Stack<Part> _parts;

    [PublicAPI]
    public static string GetPathAt(string json) => GetPathAt(Utf8.GetBytes(json));

    [PublicAPI]
    public static string GetPathAt(ReadOnlySpan<byte> json)
    {
        var path = new JsonPath(json);
        path.Build();
        return path.ToPathString();
    }

    private JsonPath(ReadOnlySpan<byte> json)
    {
        _json = json;
        _parts = new Stack<Part>();
    }

    private void Build()
    {
        while (true) {
            SkipSpaces();
            if (IsEmpty) break;

            switch (Current) {
                case Colon: {
                    Skip();
                    PushMember();
                    break;
                }

                case ArrayOpen:
                case Comma: {
                    PushItem();
                    break;
                }

                default: {
                    throw new InvalidOperationException(
                        $"Expected '[', ':', or ',', but got '{(char)Current}'"
                    );
                }
            }
        }
    }

    private void PushMember()
    {
        var property = ReadString();
        SkipObject();

        _parts.Push(new Part { Property = Utf8.GetString(property) });
    }

    private void PushItem()
    {
        var index = SkipArray();

        _parts.Push(new Part { Index = index });
    }

    private void SkipObject()
    {
        while (true) {
            SkipSpaces();

            Debug.Assert(!IsEmpty);
            switch (Current) {
                case Comma: {
                    Skip();
                    break;
                }

                case ObjectOpen: {
                    Skip();
                    return;
                }
            }

            _ = SkipDelimitedValue();
            SkipTo(Colon);
            Skip();
            _ = ReadString();
        }
    }

    private int SkipArray()
    {
        var count = 0;

        while (true) {
            SkipSpaces();

            Debug.Assert(!IsEmpty);
            switch (Current) {
                case Comma: {
                    Skip();
                    break;
                }

                case ArrayOpen: {
                    Skip();
                    return count;
                }
            }

            if (!SkipDelimitedValue()) {
                SkipTo(Comma, ArrayOpen);
            }

            ++count;
        }
    }

    private bool SkipDelimitedValue()
    {
        SkipSpaces();

        Debug.Assert(!IsEmpty);
        switch (Current) {
            case ObjectClose: {
                Skip();
                SkipObject();
                return true;
            }

            case ArrayClose: {
                Skip();
                _ = SkipArray();
                return true;
            }

            case Quote: {
                _ = ReadString();
                return true;
            }

            default: {
                return false;
            }
        }
    }

    private ReadOnlySpan<byte> ReadString()
    {
        SkipSpaces();

        Debug.Assert(Current == Quote);
        Skip();

        var save = _json;

        while (true) {
            SkipTo(Quote);
            Skip();

            var isEscaped = !IsEmpty && Current == Escape;
            if (isEscaped) {
                Skip();
            }
            else {
                break;
            }
        }

        return save[(_json.Length + 1) ..];
    }

    private bool IsEmpty => _json.IsEmpty;
    private byte Current => _json[^1];

    private void Skip()
    {
        _json = _json[.. ^1];
    }

    private void SkipSpaces()
    {
        _json = _json.TrimEnd(" \r\n\t"u8);
    }

    private void SkipTo(byte needle)
    {
        var index = _json.LastIndexOf(needle);
        Debug.Assert(index != -1);
        _json = _json[.. (index + 1)];
    }

    private void SkipTo(byte needle1, byte needle2)
    {
        var index = _json.LastIndexOfAny(needle1, needle2);
        Debug.Assert(index != -1);
        _json = _json[.. (index + 1)];
    }

    private string ToPathString()
    {
        var s = new StringBuilder("$");

        foreach (var part in _parts) {
            if (part.Index is {} i) {
                _ = s.AppendFormat(CultureInfo.InvariantCulture, $"[{i}]");
            }
            else if (part.Property is {} p) {
                if (IsSimple(p)) {
                    _ = s.AppendFormat(CultureInfo.InvariantCulture, $".{p}");
                }
                else {
                    _ = s.AppendFormat(CultureInfo.InvariantCulture, $"[\"{p}\"]");
                }
            }
        }

        return s.ToString();

        static bool IsSimple(string s) =>
            s.Length > 0 && IsSimpleStart(s[0]) && s.All(IsSimpleContinue);
        static bool IsSimpleStart(char c) => char.IsAsciiLetter(c) || c == '_';
        static bool IsSimpleContinue(char c) => IsSimpleStart(c) || char.IsAsciiDigit(c);
    }
}
