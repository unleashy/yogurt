# Yogurt

LSP-first programming language

## Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

Use [just](https://github.com/casey/just) to run dev commands.

To build for debugging, use `just build`. To run, `just run`.

To publish release binaries, use `just publish <rid>`, replacing `<rid>` with the runtime
identifier of the platform you wish to build for, such as `win-x64` or `linux-x64` as this project
uses AOT compilation.

## Licence

[MIT](LICENSE.txt)
