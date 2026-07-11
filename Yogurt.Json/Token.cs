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

internal readonly record struct Token(TokenKind Kind, int Offset, int Length);

internal readonly record struct TokenSlice(ReadOnlyMemory<Token> Tokens)
{
    public Token? First => IsEmpty ? null : Tokens.Span[0];

    public TokenSlice Skip() => IsEmpty ? this : Slice(1);

    public (TokenSlice, Token?) SkipIf(TokenKind kind) =>
        First is {} it && it.Kind == kind
            ? (Slice(1), it)
            : (this, null);

    public TokenSlice SkipIf(TokenKind kind, out bool didSkip)
    {
        didSkip = First?.Kind == kind;
        return didSkip ? Slice(1) : this;
    }

    public TokenSlice SkipIf(
        TokenKind kind1, out bool didSkip1,
        TokenKind kind2, out bool didSkip2
    )
    {
        didSkip1 = First?.Kind == kind1;
        didSkip2 = First?.Kind == kind2;
        return didSkip1 || didSkip2 ? Slice(1) : this;
    }

    public (TokenSlice before, TokenSlice after) SplitAt(int i) => (Slice(0, i), Slice(i));

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

    public (TokenSlice, Token) FindSplit(TokenKind end, out ReadOnlyMemory<Token> result)
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
        return (Slice(i + 1), span[i]);
    }

    public (TokenSlice, TokenSlice) FindSplitBalanced(TokenKind close)
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

    private bool IsEmpty => Tokens.Length == 0;

    private TokenSlice Slice(int start) => new(Tokens[start ..]);

    private TokenSlice Slice(int start, int end) => new(Tokens[start .. end]);
}
