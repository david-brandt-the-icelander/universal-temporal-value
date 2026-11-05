/** Strict JSON parser that rejects duplicate object keys. No runtime dependencies. */
export type Json = null | boolean | number | string | Json[] | { [k: string]: Json };

export class JsonError extends Error {
  constructor(message: string, readonly code: "duplicate-key" | "malformed-json", readonly path: string) {
    super(message);
  }
}

export function parseStrict(text: string): Json {
  let i = 0;
  const ws = () => { while (i < text.length && " \t\n\r".includes(text[i]!)) i++; };
  const fail = (m: string, path: string): never => { throw new JsonError(`${m} at offset ${i}`, "malformed-json", path); };
  const ptr = (base: string, key: string | number) => `${base}/${String(key).replace(/~/g, "~0").replace(/\//g, "~1")}`;
  const str = (path: string): string => {
    const start = i;
    i++;
    while (i < text.length && text[i] !== '"') {
      if (text[i] === "\\") i++;
      else if (text.charCodeAt(i) < 0x20) fail("control character in string", path);
      i++;
    }
    if (i >= text.length) fail("unterminated string", path);
    i++;
    try { return JSON.parse(text.slice(start, i)) as string; } catch { return fail("invalid string", path); }
  };
  const val = (path: string): Json => {
    ws();
    const c = text[i];
    if (c === "{") {
      i++;
      const obj: { [k: string]: Json } = Object.create(null) as { [k: string]: Json };
      const seen = new Set<string>();
      ws();
      if (text[i] === "}") { i++; return obj; }
      for (;;) {
        ws();
        if (text[i] !== '"') fail("expected key", path);
        const k = str(path);
        if (seen.has(k)) throw new JsonError(`duplicate key "${k}"`, "duplicate-key", ptr(path, k));
        seen.add(k);
        ws();
        if (text[i] !== ":") fail("expected ':'", path);
        i++;
        obj[k] = val(ptr(path, k));
        ws();
        if (text[i] === ",") { i++; continue; }
        if (text[i] === "}") { i++; return obj; }
        fail("expected ',' or '}'", path);
      }
    }
    if (c === "[") {
      i++;
      const arr: Json[] = [];
      ws();
      if (text[i] === "]") { i++; return arr; }
      for (;;) {
        arr.push(val(ptr(path, arr.length)));
        ws();
        if (text[i] === ",") { i++; continue; }
        if (text[i] === "]") { i++; return arr; }
        fail("expected ',' or ']'", path);
      }
    }
    if (c === '"') return str(path);
    for (const [lit, v] of [["true", true], ["false", false], ["null", null]] as const) {
      if (text.startsWith(lit, i)) { i += lit.length; return v; }
    }
    const m = /^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?/.exec(text.slice(i));
    if (!m) return fail("unexpected token", path);
    i += m[0].length;
    return Number(m[0]);
  };
  const out = val("");
  ws();
  if (i !== text.length) fail("trailing data", "");
  return out;
}
