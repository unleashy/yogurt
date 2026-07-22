using System.Text;
using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

public class IntegrationTests
{
    [Test]
    public async Task RequestAndResponse()
    {
        var request = new JsonRpcRequest(JsonRpcId.Int(1), "add", JsonValue.Parse("[42, 67]"));
        var response = JsonRpcResponse.Result(JsonRpcId.Int(1), JsonValue.Parse("109"));

        var transport = new FakeTransport {
            Input = [request.ToJsonString()],
        };
        var channel = new JsonRpcChannel(transport);
        var client = new JsonRpcClient(channel);
        var server = new JsonRpcServer(
            channel,
            new JsonRpcRouter.Builder {
                ["add"] = new Add(),
            }.Build()
        );
        var broker = new JsonRpcBroker(channel, new DuplexJsonRpcObserver(client, server));

        await broker.StartAsync();

        var output = transport.Output.Select(it =>
            JsonRpcParser.Parse(Encoding.UTF8.GetBytes(it)).ToMessage.ToString()
        );
        Assert.That(output, Is.EqualTo([JsonRpcMessage.Response(response).ToString()]));
    }

    [Test]
    public async Task Notification()
    {
        var request = new JsonRpcRequest(id: null, "add", JsonValue.Parse("[-3.14, 3.14]"));

        var transport = new FakeTransport {
            Input = [request.ToJsonString()],
        };
        var channel = new JsonRpcChannel(transport);
        var client = new JsonRpcClient(channel);
        var add = new Add();
        var server = new JsonRpcServer(
            channel,
            new JsonRpcRouter.Builder {
                ["add"] = add,
            }.Build()
        );
        var broker = new JsonRpcBroker(channel, new DuplexJsonRpcObserver(client, server));

        await broker.StartAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(transport.Output, Is.Empty);
            Assert.That(add.Invocations, Is.EqualTo([
                (new Add.Input(-3.14, 3.14), new Add.Output(0)),
            ]));
        }
    }

    [Test]
    public async Task ClientUsage()
    {
        var id = JsonRpcId.String("🐇");
        var input = new Add.Input(5, 5);
        var request = new JsonRpcRequest(id, "add", input.ToJson());
        var expectedResponse = JsonRpcResponse.Result(id, JsonValue.Parse("10"));

        var transport = new FakeTransport() {
            Input = [expectedResponse.ToJsonString()],
        };
        var channel = new JsonRpcChannel(transport);
        var client = new JsonRpcClient(channel) { IdGenerator = () => id };
        var server = new JsonRpcServer(
            channel,
            new JsonRpcRouter.Builder {
                ["add"] = new Add(),
            }.Build()
        );
        var broker = new JsonRpcBroker(channel, new DuplexJsonRpcObserver(client, server));

        var pending = client.InvokeAsync(request.Method, request.Params);
        var completion = broker.StartAsync();
        var response = await pending;
        await completion;

        var output = transport.Output.Select(it =>
            JsonRpcParser.Parse(Encoding.UTF8.GetBytes(it)).ToMessage.ToString()
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo([JsonRpcMessage.Request(request).ToString()]));
            Assert.That(response.ToString(), Is.EqualTo(expectedResponse.ToString()));
        }
    }
}

internal sealed class Add : JsonRpcMethod<Add.Input, Add.Output>
{
    public List<(Input, Output)> Invocations { get; } = [];

    public override ValueTask<Output> InvokeAsync(
        Input input,
        CancellationToken cancellationToken = default
    )
    {
        var output = new Output(input.A + input.B);
        Invocations.Add((input, output));
        return ValueTask.FromResult(output);
    }

    public readonly record struct Input(double A, double B) : IJsonable<Input>
    {
        public static Input Parse(in JsonValue json)
        {
            var xs = json.Array(static it => it.Number<double>());
            return new Input(xs[0], xs[1]);
        }

        public void ToJson(JsonBuilder json)
        {
            var self = this;
            json.Array(it => {
                it.Number(self.A);
                it.Number(self.B);
            });
        }
    }

    public readonly record struct Output(double Value) : IJsonBuildable
    {
        public void ToJson(JsonBuilder json)
        {
            json.Number(Value);
        }
    }
}
