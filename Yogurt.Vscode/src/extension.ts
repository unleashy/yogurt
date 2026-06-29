import * as vscode from "vscode";

export function activate(context: vscode.ExtensionContext) {
  console.log('Congratulations, your extension "yogurt" is now active!');

  const disposable = vscode.commands.registerCommand("yogurt.helloWorld", () => {
    vscode.window.showInformationMessage("Hello World from yogurt!");
  });

  context.subscriptions.push(disposable);
}

export function deactivate() {}
