using Yogurt.Server;

var result = Cli.Parse(args);
if (result.TryInvokeAction(out var exitCode)) {
    return exitCode;
}

Console.WriteLine($"Client process ID = {result.ClientProcessId?.ToString() ?? "null"}");
Console.WriteLine($"Transport = {result.Transport}");

return 0;
