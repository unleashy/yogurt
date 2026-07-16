namespace Yogurt.Json;

internal static class SpanExtensions
{
    public static int IndexOf(this ReadOnlySpan<byte> span, Predicate<char> predicate)
    {
        for (var i = 0; i < span.Length; ++i) {
            if (predicate((char)span[i])) {
                return i;
            }
        }

        return -1;
    }
}
