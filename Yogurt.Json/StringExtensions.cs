using System.Text;
using System.Text.Encodings.Web;

namespace Yogurt.Json;

internal static class StringExtensions
{
    public static string JsonEscape(this string s)
    {
        var sb = new StringBuilder("\"", capacity: 2 + s.Length);

        JavaScriptEncoder.UnsafeRelaxedJsonEscaping.Encode(new StringWriter(sb), s);

        return sb.Append('"').ToString();
    }
}
