import * as vscode from "vscode";
import {
  LanguageClient,
  type LanguageClientOptions,
  type ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  let config = vscode.workspace.getConfiguration("yogurt");

  let serverPath = config.get("serverPath") as string;
  console.log(`[yogurt] ${serverPath}`);

  let serverOptions: ServerOptions = {
    command: serverPath,
    transport: TransportKind.stdio,
  };

  let clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "yogurt" }],
  };

  client = new LanguageClient("yogurt", "Yogurt", serverOptions, clientOptions);

  await client.start();
}

export function deactivate(): Promise<void> | undefined {
  // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
  if (client) {
    return client.stop();
  }
}
