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

internal readonly record struct TokenSlice(ReadOnlyMemory<byte> Text, ReadOnlyMemory<Token> Tokens)
{
    public bool IsEmpty => Tokens.Length == 0;

    public Token? First => IsEmpty ? null : Tokens.Span[0];

    public TokenSlice Slice(int start) => new(Text, Tokens[start ..]);

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

    public (TokenSlice, Token) FindSplit(TokenKind end, out ReadOnlySpan<Token> result)
    {
        var span = Tokens.Span;
        var i = 0;
        while (i < span.Length) {
            if (span[i].Kind == end) {
                break;
            }

            ++i;
        }

        result = span[.. i];
        return (Slice(i + 1), span[i]);
    }
}
