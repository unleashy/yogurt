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

internal readonly struct TokenSlice
{
    private readonly ReadOnlyMemory<Token> _tokens;

    public TokenSlice(ReadOnlyMemory<Token> tokens)
    {
        if (tokens.Length == 0) {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(tokens),
                message: "Cannot create an empty TokenSlice",
                actualValue: tokens.Length
            );
        }

        _tokens = tokens;
    }

    public Token First => _tokens.Span[0];

    public bool Has(TokenKind kind) => First.Kind == kind;

    public Token? Match(TokenKind kind) =>
        First is {} token && token.Kind == kind ? token : null;

    public TokenSlice Skip() => Slice(1);

    public TokenSlice SkipIf(TokenKind kind, out bool didSkip)
    {
        didSkip = Has(kind);
        return didSkip ? Slice(1) : this;
    }

    public (TokenSlice, TokenSlice) SplitAt(int i) => (Slice(0, i), Slice(i));

    public (TokenSlice, TokenSlice) FindSplit(TokenKind needle)
    {
        var span = _tokens.Span;
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
        var span = _tokens.Span;
        var i = 0;
        while (i < span.Length) {
            if (span[i].Kind == end) {
                break;
            }

            ++i;
        }

        result = _tokens[.. i];
        return span[i];
    }

    public (TokenSlice Before, TokenSlice After) FindSplitBalanced(TokenKind close)
    {
        var span = _tokens.Span;
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
        First.Kind switch {
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

    private TokenSlice Slice(int start) => Slice(start, _tokens.Length);

    private TokenSlice Slice(int start, int end)
    {
        var newTokens = _tokens[start .. end];
        return newTokens.Length > 0
            ? new TokenSlice(newTokens)
            : throw new ArgumentException(
                  $"Given range ({start} .. {end}) would create an empty TokenSlice " +
                  $"(current length: {_tokens.Length})"
              );
    }
}
