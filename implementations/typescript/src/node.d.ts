// The one Node built-in the library uses (ZoneRules.load reads a file the caller names). Declared here
// so the package needs no @types/node.
declare module "node:fs" {
  export function readFileSync(path: string | URL, enc: "utf8"): string;
  export function readdirSync(path: string | URL): string[];
}
