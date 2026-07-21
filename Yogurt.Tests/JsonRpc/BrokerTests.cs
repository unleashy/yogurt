using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

public class BrokerTests
{
    [Test]
    public async Task RoutesMessages()
    {
        var channel = new FakeChannel();
        var observer = new FakeObserver();
        var sut = new JsonRpcBroker(channel, observer);
        var request = new JsonRpcRequest { Method = "method" };
        var response = JsonRpcResponse.Result(JsonRpcId.Int(1), JsonValues.True);

        var completion = sut.StartAsync();
        await channel.InputWriter.WriteAsync(JsonRpcMessage.Request(request));
        await channel.InputWriter.WriteAsync(JsonRpcMessage.Response(response));
        channel.Complete();
        await completion;

        using (Assert.EnterMultipleScope()) {
            Assert.That(observer.Requests, Is.EqualTo([request]));
            Assert.That(observer.Responses, Is.EqualTo([response]));
            Assert.That(observer.Completions, Is.EqualTo(1));
        }
    }
}

internal sealed class FakeObserver : IJsonRpcObserver
{
    public List<JsonRpcRequest> Requests { get; } = new();
    public List<JsonRpcResponse> Responses { get; } = new();
    public int Completions { get; private set; }

    public ValueTask<bool> OnRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Requests.Add(request);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> OnResponseAsync(
        JsonRpcResponse response,
        CancellationToken cancellationToken = default
    )
    {
        Responses.Add(response);
        return ValueTask.FromResult(true);
    }

    public void OnComplete()
    {
        ++Completions;
    }
}
