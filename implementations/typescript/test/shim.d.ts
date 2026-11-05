// Minimal ambient declarations for Node built-ins used by the tests (avoids an @types/node dependency).
declare module "node:test" {
  export function test(name: string, fn: () => void | Promise<void>): void;
}
declare module "node:assert/strict" {
  const assert: {
    equal(a: unknown, b: unknown, msg?: string): void;
    ok(v: unknown, msg?: string): void;
    notEqual(a: unknown, b: unknown, msg?: string): void;
  };
  export default assert;
}
declare module "node:fs" {
  export function readFileSync(path: string | URL, enc: "utf8"): string;
}
