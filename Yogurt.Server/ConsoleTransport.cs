using System.IO.Pipelines;

namespace Yogurt.Server;

public sealed class ConsoleTransport : IDuplexPipe
{
    public PipeReader Input { get; } = PipeReader.Create(
        Console.OpenStandardInput(),
        new StreamPipeReaderOptions(leaveOpen: true)
    );

    public PipeWriter Output { get; } = PipeWriter.Create(
        Console.OpenStandardOutput(),
        new StreamPipeWriterOptions(leaveOpen: true)
    );
}
