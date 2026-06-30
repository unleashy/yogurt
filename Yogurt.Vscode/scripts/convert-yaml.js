import path from "node:path";
import * as fs from "node:fs/promises";
import * as yaml from "yaml";

async function convert(from, to) {
  let source = await fs.readFile(from, "utf8");

  let grammar = yaml.parse(source, { strict: true, stringKeys: true });

  let output = JSON.stringify(grammar);

  await fs.mkdir(path.dirname(to), { recursive: true });
  await fs.writeFile(to, output);
}

await Promise.all([
  convert("./language/yogurt.tmLanguage.yaml", "./out/syntaxes/yogurt.tmLanguage.json"),
  convert("./language/configuration.yaml", "./out/language-configuration.json"),
]);
