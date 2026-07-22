using System.Buffers;
using System.Globalization;
using System.Text;

namespace Yogurt.Json;

public static class StringExtensions
{
    [PublicAPI]
    public static string JsonEscape(this ReadOnlySpan<char> s)
    {
        var sb = new StringBuilder("\"", capacity: 2 + s.Length);
        JsonEscaper.Instance.Escape(s, sb);
        return sb.Append('"').ToString();
    }
}

internal sealed class JsonEscaper
{
    public static JsonEscaper Instance => _instance.Value;
    private static Lazy<JsonEscaper> _instance =
        new(() => new JsonEscaper(), LazyThreadSafetyMode.PublicationOnly);

    private SearchValues<char> _escapeSearcher = CreateEscapeSearcher();

    public void Escape(ReadOnlySpan<char> s, StringBuilder b)
    {
        while (true) {
            var nextIndex = s.IndexOfAny(_escapeSearcher);
            if (nextIndex == -1) break;

            var prev = s[.. nextIndex];
            var c = s[nextIndex];
            var next = s[(nextIndex + 1) ..];

            _ = b.Append(prev);
            _ = c switch {
                '"'  => b.Append(@"\"""),
                '\\' => b.Append(@"\\"),
                '\b' => b.Append(@"\b"),
                '\f' => b.Append(@"\f"),
                '\n' => b.Append(@"\n"),
                '\r' => b.Append(@"\r"),
                '\t' => b.Append(@"\t"),
                _    => b.AppendFormat(CultureInfo.InvariantCulture, $@"\u{(int)c:X4}"),
            };
            s = next;
        }

        _ = b.Append(s);
    }

    private static SearchValues<char> CreateEscapeSearcher()
    {
        const int numControls = 32;
        const int numQuoteAndSlash = 2;

        Span<char> values = stackalloc char[numControls + numQuoteAndSlash];

        for (var i = 0; i < numControls; ++i) {
            values[i] = (char)i;
        }

        values[^2] = '"';
        values[^1] = '\\';

        return SearchValues.Create(values);
    }
}
