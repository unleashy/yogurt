using System.Text;

namespace Yogurt.Json;

internal static class Utf8
{
    private static readonly UTF8Encoding Encoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    public static byte[] GetBytes(string s) => Encoding.GetBytes(s);

    public static string GetString(ReadOnlySpan<byte> bytes) => Encoding.GetString(bytes);

    public static bool TryGetChars(
        ReadOnlySpan<byte> bytes,
        Span<char> chars,
        out int charsWritten
    )
    {
        return Encoding.TryGetChars(bytes, chars, out charsWritten);
    }
}
