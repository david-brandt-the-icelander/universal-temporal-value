// Minimal ambient declarations for Node built-ins used by the tests and examples (avoids an @types/node dependency).
// `node:fs` is declared in src/node.d.ts.
declare module "node:test" {
  export function test(name: string, fn: () => void | Promise<void>): void;
}
declare module "node:assert/strict" {
  const assert: {
    equal(a: unknown, b: unknown, msg?: string): void;
    ok(v: unknown, msg?: string): asserts v;
    notEqual(a: unknown, b: unknown, msg?: string): void;
    deepEqual(a: unknown, b: unknown, msg?: string): void;
    throws(fn: () => unknown, expected?: RegExp | (new (...args: never[]) => unknown) | ((e: unknown) => boolean), msg?: string): void;
  };
  export default assert;
}
declare module "node:url" {
  export function pathToFileURL(path: string): URL;
  export function fileURLToPath(url: URL): string;
}
declare module "node:child_process" {
  export function execFileSync(file: string, args: string[], options: { cwd?: string; encoding: "utf8"; env?: Record<string, string> }): string;
}
declare const process: { argv: string[]; exitCode: number | undefined; execPath: string; env: Record<string, string | undefined> };
