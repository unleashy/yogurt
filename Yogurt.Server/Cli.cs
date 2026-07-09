using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Yogurt.Server;

internal sealed class Cli
{
    public readonly struct Parsing(Cli cli, ParseResult parseResult)
    {
        [PublicAPI]
        public bool TryInvokeAction(out int exitCode)
        {
            exitCode = parseResult.Invoke();
            return parseResult.Action is not null;
        }

        [PublicAPI]
        public uint? ClientProcessId => parseResult.GetValue(cli._clientProcessId);

        [PublicAPI]
        public TransportOption Transport {
            get {
                Debug.Assert(!cli.HasConflictingTransportOptions(parseResult.RootCommandResult));

                if (parseResult.GetValue(cli._pipe) is {} fileInfo) {
                    return new TransportOption.Pipe(fileInfo);
                }
                else if (parseResult.GetValue(cli._socket) is {} port) {
                    return new TransportOption.Socket(port);
                }
                else {
                    return new TransportOption.Console();
                }
            }
        }
    }

    [PublicAPI]
    public abstract record TransportOption
    {
        public sealed record Console : TransportOption;
        public sealed record Socket(ushort Port) : TransportOption;
        public sealed record Pipe(FileInfo Path) : TransportOption;
    }

    private readonly RootCommand _command;

    private readonly Option<uint?> _clientProcessId = new("--clientProcessId") {
        Description = "The process ID of the client using this LSP",
        HelpName = "pid",
    };

    private readonly Option<bool?> _stdio = new("--stdio") {
        Description =
            """
            Use standard input/output to communicate with the client. If no communication
            flag is supplied, this is the default.
            """,
        Arity = ArgumentArity.Zero,
    };

    private readonly Option<FileInfo?> _pipe = new("--pipe") {
        Description = "Use the given named pipe/domain socket to communicate with the client.",
        HelpName = "path",
    };

    private readonly Option<ushort?> _socket = new("--socket") {
        Description =
            """
            Use a TCP socket at localhost on the given port to communicate with
            the client.
            """,
        HelpName = "port",
    };

    [PublicAPI]
    public static Parsing Parse(IReadOnlyList<string> args)
    {
        var cli = new Cli();
        var parseResult = cli._command.Parse(args);
        return new Parsing(cli, parseResult);
    }

    private Cli()
    {
        _command = new RootCommand("Yogurt LSP server") {
            _clientProcessId,
            _stdio,
            _pipe.AcceptLegalFilePathsOnly(),
            _socket,
        };
        _command.Validators.Add(Validate);
    }

    private void Validate(CommandResult cli)
    {
        if (HasConflictingTransportOptions(cli)) {
            cli.AddError("Only one of --stdio, --pipe, or --socket may be specified at once.");
        }
    }

    private bool HasConflictingTransportOptions(CommandResult cli)
    {
        var a = cli.GetResult(_stdio);
        var b = cli.GetResult(_pipe);
        var c = cli.GetResult(_socket);

        return (a != null && b != null) || (a != null && c != null) || (b != null && c != null);
    }
}
