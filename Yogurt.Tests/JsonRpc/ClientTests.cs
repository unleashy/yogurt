using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

public class ClientTests
{
    [Test]
    public async Task Invoke_WritesRequest_AwaitsResponse()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel) { IdGenerator = () => JsonRpcId.Int(0) };
        var request = new JsonRpcRequest { Id = sut.IdGenerator(), Method = ":p" };

        var task = sut.InvokeAsync(request.Method);
        var message = await channel.OutputReader.ReadAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(message, Is.EqualTo(JsonRpcMessage.Request(request)));
            Assert.That(task.IsCompleted, Is.False);
        }
    }

    [Test]
    public async Task Invoke_WritesRequest_ReceivesResponse()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel) { IdGenerator = () => JsonRpcId.Int(0) };
        var request = new JsonRpcRequest { Id = sut.IdGenerator(), Method = ":p" };
        var response = JsonRpcResponse.Result(sut.IdGenerator(), JsonValues.True);

        var task = sut.InvokeAsync(request.Method);
        var message = await channel.OutputReader.ReadAsync();
        var handled = await sut.OnResponseAsync(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(handled, Is.True);
            Assert.That(message, Is.EqualTo(JsonRpcMessage.Request(request)));
            Assert.That(await task, Is.EqualTo(response));
        }
    }

    [Test]
    public async Task Invoke_EarlyCancellation_Fails()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel);
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();
        var task = sut.InvokeAsync("cancel", cancellationToken: cts.Token);

        using (Assert.EnterMultipleScope()) {
            await Assert.ThatAsync(
                () => task,
                Throws
                    .InstanceOf<OperationCanceledException>().And
                    .Property("CancellationToken").EqualTo(cts.Token)
            );
            Assert.That(channel.OutputReader.Count, Is.Zero);
        }
    }

    [Test]
    public async Task Invoke_MidwayCancellation_Fails()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel);
        using var cts = new CancellationTokenSource();

        var task = sut.InvokeAsync("cancel", cancellationToken: cts.Token);
        await cts.CancelAsync();

        await Assert.ThatAsync(
            () => task,
            Throws
                .InstanceOf<OperationCanceledException>().And
                .Property("CancellationToken").EqualTo(cts.Token)
        );
    }

    [Test]
    public async Task OnResponse_UnknownId_Rejects()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel);

        var handled = await sut.OnResponseAsync(
            JsonRpcResponse.Result(JsonRpcId.Int(1234), JsonValues.False)
        );

        Assert.That(handled, Is.False);
    }

    [Test]
    public async Task Notify_WritesRequest()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel);

        await sut.NotifyAsync("➡️");

        var message = await channel.OutputReader.ReadAsync();
        Assert.That(message, Is.EqualTo(
            JsonRpcMessage.Request(new JsonRpcRequest { Method = "➡️" })
        ));
    }

    [Test]
    public async Task OnRequest_Rejects()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel);

        var handled = await ((IJsonRpcObserver) sut).OnRequestAsync(default);

        Assert.That(handled, Is.False);
    }

    [Test]
    public void OnComplete_CompletesInput()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcClient(channel);

        sut.OnComplete();

        Assert.That(channel.OutputReader.Completion.IsCompletedSuccessfully, Is.True);
    }
}
