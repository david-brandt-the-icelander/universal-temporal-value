// Minimal ambient declarations for Node built-ins used by the tests (avoids an @types/node dependency).
// `node:fs` is declared in src/node.d.ts.
declare module "node:test" {
  export function test(name: string, fn: () => void | Promise<void>): void;
  export function describe(name: string, fn: () => void): void;
  export function it(name: string, fn: () => void | Promise<void>): void;
}
declare module "node:assert/strict" {
  const assert: {
    equal(a: unknown, b: unknown, msg?: string): void;
    ok(v: unknown, msg?: string): asserts v;
    notEqual(a: unknown, b: unknown, msg?: string): void;
    deepEqual(a: unknown, b: unknown, msg?: string): void;
    match(s: string, re: RegExp, msg?: string): void;
    throws(fn: () => unknown, expected?: RegExp | (new (...args: never[]) => unknown) | ((e: unknown) => boolean), msg?: string): void;
  };
  export default assert;
}
