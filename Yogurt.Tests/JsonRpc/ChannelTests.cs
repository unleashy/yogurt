using System.Text;
using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

public class ChannelTests
{
    [Test]
    public async Task WritesArePushed()
    {
        var transport = new FakeTransport();
        var sut = new JsonRpcChannel(transport);

        var completion = sut.StartAsync();
        await sut.Output.WriteAsync(JsonRpcMessage.Request(new JsonRpcRequest { Method = "1" }));
        await sut.Output.WriteAsync(JsonRpcMessage.Request(new JsonRpcRequest { Method = "2" }));
        await sut.Output.WriteAsync(JsonRpcMessage.Request(new JsonRpcRequest { Method = "3" }));
        sut.Output.Complete();
        await completion;

        Assert.That(transport.Output, Is.EqualTo([
            """{"jsonrpc":"2.0","method":"1"}""",
            """{"jsonrpc":"2.0","method":"2"}""",
            """{"jsonrpc":"2.0","method":"3"}""",
        ]));
    }

    [Test]
    public async Task ReadsArePulled()
    {
        var transport = new FakeTransport {
            Input = [
                """{"jsonrpc":"2.0","method":"1"}""",
                """{"jsonrpc":"2.0","method":"2"}""",
                """{"jsonrpc":"2.0","method":"3"}""",
            ],
        };
        var sut = new JsonRpcChannel(transport);
        sut.Output.Complete();

        var completion = sut.StartAsync();
        var input = await sut.Input.ReadAllAsync().ToArrayAsync();
        await completion;

        Assert.That(input, Is.EqualTo([
            JsonRpcMessage.Request(new JsonRpcRequest { Method = "1" }),
            JsonRpcMessage.Request(new JsonRpcRequest { Method = "2" }),
            JsonRpcMessage.Request(new JsonRpcRequest { Method = "3" }),
        ]));
    }

    [Test]
    public async Task DuplexCommunication()
    {
        var transport = new FakeTransport {
            Input = [
                """{"jsonrpc":"2.0","id":1,"method":"ping"}""",
                """{"jsonrpc":"2.0","id":2,"method":"ping"}""",
                """{"jsonrpc":"2.0","id":3,"method":"ping"}""",
            ],
        };
        var sut = new JsonRpcChannel(transport);

        var completion = sut.StartAsync();
        await foreach (var message in sut.Input.ReadAllAsync()) {
            var id = message.ToRequest.Id!.Value;
            await sut.Output.WriteAsync(
                JsonRpcMessage.Response(JsonRpcResponse.Result(id, JsonValue.Parse("\"pong\"")))
            );
        }
        sut.Output.Complete();
        await completion;

        Assert.That(transport.Output, Is.EqualTo([
            """{"jsonrpc":"2.0","id":1,"result":"pong"}""",
            """{"jsonrpc":"2.0","id":2,"result":"pong"}""",
            """{"jsonrpc":"2.0","id":3,"result":"pong"}""",
        ]));
    }

    [Test]
    public async Task MessageParseError()
    {
        var transport = new FakeTransport {
            Input = ["{}"],
        };
        var sut = new JsonRpcChannel(transport);

        var completion = sut.StartAsync();
        sut.Output.Complete();
        await completion;

        using (Assert.EnterMultipleScope()) {
            Assert.That(transport.Output, Is.EqualTo([
                """{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"$: Missing required keys: \"jsonrpc\" (at 1:1)"}}""",
            ]));
        }
    }
}

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
