import * as vscode from "vscode";

export function activate(context: vscode.ExtensionContext) {
  let serverPath = vscode.workspace.getConfiguration("yogurt").get("serverPath") as string;
  console.log(`[yogurt] ${serverPath}`);
}
