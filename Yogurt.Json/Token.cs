namespace Yogurt.Json;

internal enum TokenKind : byte
{
    Null,
    BoolTrue,
    BoolFalse,
    Number,
    StringSimple,
    StringComplexStart,
    StringEscape,
    StringEscapeUnicode,
    StringEscapeUnicodePair,
    StringComplexEnd,
    ArrayOpen,
    ArrayClose,
    ObjectOpen,
    ObjectClose,
}

internal readonly record struct Token(TokenKind Kind, int Offset, int Length)
{
    public ReadOnlySpan<byte> Text(ReadOnlyMemory<byte> text) => text.Span.Slice(Offset, Length);
}

internal readonly record struct TokenSlice(ReadOnlyMemory<Token> Tokens)
{
    public Token? First => IsEmpty ? null : Tokens.Span[0];

    public bool Has(TokenKind kind) => First?.Kind == kind;

    public Token? Match(TokenKind kind) =>
        First is {} token && token.Kind == kind ? token : null;

    public TokenSlice Skip() => IsEmpty ? this : Slice(1);

    public TokenSlice SkipIf(TokenKind kind, out bool didSkip)
    {
        didSkip = Has(kind);
        return didSkip ? Slice(1) : this;
    }

    public (TokenSlice, TokenSlice) SplitAt(int i) => (Slice(0, i), Slice(i));

    public (TokenSlice, TokenSlice) FindSplit(TokenKind needle)
    {
        var span = Tokens.Span;
        var i = 0;
        while (i < span.Length) {
            if (span[i++].Kind == needle) {
                break;
            }
        }

        return (Slice(0, i), Slice(i + 1));
    }

    public Token FindSplit(TokenKind end, out ReadOnlyMemory<Token> result)
    {
        var span = Tokens.Span;
        var i = 0;
        while (i < span.Length) {
            if (span[i].Kind == end) {
                break;
            }

            ++i;
        }

        result = Tokens[.. i];
        return span[i];
    }

    public (TokenSlice Before, TokenSlice After) FindSplitBalanced(TokenKind close)
    {
        var span = Tokens.Span;
        if (span.Length == 0) throw new InvalidOperationException("Empty token slice");

        var open = span[0].Kind;

        var level = 1;
        var i = 1;
        while (i < span.Length && level > 0) {
            if (span[i].Kind == open) {
                ++level;
            }
            else if (span[i].Kind == close) {
                --level;
            }

            ++i;
        }

        return level == 0
            ? SplitAt(i)
            : throw new InvalidOperationException("Unbalanced token slice");
    }

    public (TokenSlice Before, TokenSlice After) SkipValue() =>
        First?.Kind switch {
            TokenKind.Null or
            TokenKind.BoolTrue or
            TokenKind.BoolFalse or
            TokenKind.Number or
            TokenKind.StringSimple =>
                SplitAt(1),

            TokenKind.StringComplexStart => FindSplit(TokenKind.StringComplexEnd),
            TokenKind.ArrayOpen => FindSplitBalanced(TokenKind.ArrayClose),
            TokenKind.ObjectOpen => FindSplitBalanced(TokenKind.ObjectClose),

            _ => throw new InvalidOperationException("TokenSlice is empty"),
        };

    private bool IsEmpty => Tokens.Length == 0;

    private TokenSlice Slice(int start) => new(Tokens[start ..]);

    private TokenSlice Slice(int start, int end) => new(Tokens[start .. end]);
}
