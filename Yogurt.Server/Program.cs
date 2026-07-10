using System.Runtime.CompilerServices;
using Yogurt.Server;

var result = Cli.Parse(args);
if (result.TryInvokeAction(out var exitCode)) {
    return exitCode;
}

var transport = result.Transport switch {
    Cli.TransportOption.Console => new ConsoleTransport(),
    Cli.TransportOption.Pipe    => throw new NotImplementedException(),
    Cli.TransportOption.Socket  => throw new NotImplementedException(),
    _ => throw new SwitchExpressionException(result.Transport),
};

var reader = new ProtocolReader(transport.Input);

await foreach (var message in reader.ReadAllAsync()) {
    Console.Error.WriteLine(message);
}

return 0;
