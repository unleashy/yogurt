using System.Text;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

internal sealed class FakeTransport : IJsonRpcTransport
{
    public IEnumerable<string> Input { get; init; } = Enumerable.Empty<string>();

    private List<string> _output = [];
    public IReadOnlyList<string> Output => _output;

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Input
            .Select(m => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(m)))
            .ToAsyncEnumerable();
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken = default
    )
    {
        _output.Add(Encoding.UTF8.GetString(message.Span));
        return ValueTask.CompletedTask;
    }
}
