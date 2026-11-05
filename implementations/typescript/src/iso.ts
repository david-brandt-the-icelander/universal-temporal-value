/** ISO 8601 extended / RFC 3339 / RFC 9557 parsing and formatting (docs/everyday-api.md, `parseIso`, `formatIso`). */
import type { Json } from "./json.js";
import type { UtvResult } from "./result.js";
import { civilFromDays, daysInMonth } from "./civil.js";
import { split, toNano } from "./rational.js";
import {
  envelope, fail, readCivil, readInstant, run, unique, wallSeconds, writeCivil, writeInstant,
  MAX_OFFSET, type Civil, type Zone,
} from "./internal.js";

const Y = String.raw`([+-]\d{4,}|\d{4})`;
const T = String.raw`(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,9}))?)?`;
const RE_YEAR_MONTH = new RegExp(`^${Y}-(\\d{2})$`);
const RE_DATE = new RegExp(`^${Y}-(\\d{2})-(\\d{2})$`);
const RE_TIME = new RegExp(`^${T}$`);
const RE_DATETIME = new RegExp(`^${Y}-(\\d{2})-(\\d{2})T${T}(Z|[+-]\\d{2}:\\d{2})?((?:\\[[^\\[\\]]*\\])*)$`);
export const ZONE_NAME = /^[A-Za-z._][A-Za-z0-9._+-]*(\/[A-Za-z._][A-Za-z0-9._+-]*)*$/;

/** Valid ISO 8601 / RFC forms that this profile recognises but does not handle. */
const UNSUPPORTED_FORMS: [RegExp, string][] = [
  [new RegExp(`^${Y}-?W\\d{2}(-?\\d)?$`), "week dates are not supported"],
  [new RegExp(`^${Y}-\\d{3}$`), "ordinal dates are not supported"],
  [/^([+-]\d{5,}|\d{7})$/, "basic-format ordinal dates are not supported"],
  [/^([+-]\d{8,}|\d{8})(T\d{2}(\d{2}(\d{2}([.,]\d+)?)?)?(Z|[+-]\d{2}(\d{2})?)?)?$/, "basic format is not supported"],
];

const yearOf = (s: string, path: string): bigint => {
  if (/^-0+$/.test(s)) fail("invalid", "syntax", path, "year -0000 is not allowed");
  return BigInt(s);
};

/** Parses ISO 8601 extended / RFC 3339 / RFC 9557 text into a UTV value. */
export function parseIso(text: string): UtvResult {
  return run("parseIso", () => {
    if (typeof text !== "string") fail("invalid", "type", "", "parseIso takes a string");
    const bad = (message: string) => fail("invalid", "syntax", "", message);
    let m: RegExpExecArray | null;
    if ((m = RE_YEAR_MONTH.exec(text))) {
      const c = civil("year-month", yearOf(m[1]!, ""), +m[2]!, 1);
      return unique(writeCivil(c));
    }
    if ((m = RE_DATE.exec(text))) return unique(writeCivil(civil("date", yearOf(m[1]!, ""), +m[2]!, +m[3]!)));
    if ((m = RE_TIME.exec(text))) {
      const c = civil("time", 0n, 1, 1);
      setTime(c, m.slice(1, 5));
      return unique(writeCivil(c));
    }
    if ((m = RE_DATETIME.exec(text))) {
      const c = civil("datetime", yearOf(m[1]!, ""), +m[2]!, +m[3]!);
      setTime(c, m.slice(4, 8));
      const off = m[8], ann = m[9] ?? "";
      let offset: number | undefined;
      if (off !== undefined && off !== "Z") {
        const [hh, mm] = [+off.slice(1, 3), +off.slice(4, 6)];
        offset = (off[0] === "-" ? -1 : 1) * (hh * 3600 + mm * 60);
        if (mm > 59 || Math.abs(offset) > MAX_OFFSET) fail("invalid", "offset-out-of-range", "", `offset ${off} is beyond ±18:00`);
      }
      const annotations = ann === "" ? [] : ann.slice(1, -1).split("][");
      if (annotations.some((a) => a.startsWith("!"))) fail("unsupported", "unsupported-annotation", "", "critical annotations [!…] are not supported");
      if (annotations.length > 1 || annotations.some((a) => !ZONE_NAME.test(a))) {
        if (annotations.some((a) => a === "" || /[^A-Za-z0-9._+\-/=:]/.test(a))) bad("malformed annotation");
        fail("unsupported", "unsupported-annotation", "", "only a single [Area/Location] time zone annotation is supported");
      }
      checkTime(c);
      const zone = annotations[0];
      const utc = off === "Z" || off === "-00:00";
      if (utc && zone !== undefined) fail("unsupported", "unsupported-annotation", "", "Z or -00:00 followed by a [zone] is not supported");
      if (utc) return unique(writeInstant(wallSeconds(c)));
      if (zone !== undefined) {
        c.zone = { type: "iana", id: zone };
        if (offset !== undefined) c.offsetAssertion = offset;
      } else if (offset !== undefined) {
        c.zone = { type: "fixed-offset", seconds: offset };
      }
      return unique(writeCivil(c));
    }
    for (const [re, message] of UNSUPPORTED_FORMS) if (re.test(text)) fail("unsupported", "unsupported-form", "", message);
    return bad(`not an ISO 8601 extended / RFC 3339 / RFC 9557 form this profile reads: ${JSON.stringify(text)}`);
  });
}

function civil(form: Civil["form"], year: bigint, month: number, day: number): Civil {
  if (month < 1 || month > 12) fail("invalid", "month-out-of-range", "", `month ${month} is out of range`);
  if (day < 1 || day > daysInMonth(year, month)) fail("invalid", "day-out-of-range", "", `${year}-${String(month).padStart(2, "0")} has no day ${day}`);
  return { form, year, month, day, hour: 0, minute: 0, second: 0, nano: 0, hasSecond: false, zone: { type: "floating" } };
}

function setTime(c: Civil, [h, mi, s, f]: (string | undefined)[]): void {
  c.hour = +h!; c.minute = +mi!;
  if (s !== undefined) { c.second = +s; c.hasSecond = true; }
  if (f !== undefined) c.nano = +f.padEnd(9, "0");
  if (c.minute > 59) fail("invalid", "field-out-of-range", "", `minute ${c.minute} is out of range`);
  if (c.second > 60) fail("invalid", "field-out-of-range", "", `second ${c.second} is out of range`);
  if (c.hour > 24 || (c.hour === 24 && (c.minute !== 0 || c.second !== 0 || c.nano !== 0))) fail("invalid", "field-out-of-range", "", `hour ${c.hour} is out of range`);
  if (c.form === "time") checkTime(c);
}

/** The valid-but-unhandled time forms, checked after every `invalid` check. */
function checkTime(c: Civil): void {
  if (c.second === 60) fail("unsupported", "leap-second", "", "second 60 needs a leap-second table");
  if (c.hour === 24) fail("unsupported", "end-of-day", "", "24:00 needs an end-of-day policy");
}

// ---------------------------------------------------------------------------------------------

const pad = (n: number, w = 2) => String(n).padStart(w, "0");

function formatYear(y: bigint): string {
  if (y >= 0n && y <= 9999n) return y.toString().padStart(4, "0");
  return (y < 0n ? "-" : "+") + (y < 0n ? -y : y).toString().padStart(4, "0");
}

const fraction = (nano: number) => (nano === 0 ? "" : "." + pad(nano, 9).replace(/0+$/, ""));

function formatTime(c: Civil, forceSeconds = false): string {
  let s = `${pad(c.hour)}:${pad(c.minute)}`;
  if (c.hasSecond || forceSeconds) s += `:${pad(c.second)}${fraction(c.nano)}`;
  return s;
}

function formatOffset(seconds: number, path: string): string {
  if (seconds % 60 !== 0) fail("unsupported", "no-text-form", path, "offsets with seconds have no ISO 8601 extended form here");
  const a = Math.abs(seconds);
  return `${seconds < 0 ? "-" : "+"}${pad(Math.floor(a / 3600))}:${pad((a % 3600) / 60)}`;
}

function formatZone(zone: Zone, offsetAssertion: number | undefined): string {
  if (zone.type === "fixed-offset") {
    if (offsetAssertion !== undefined) fail("unsupported", "no-text-form", "/value/offsetAssertion", "offsetAssertion on a fixed-offset value has no ISO form");
    return formatOffset(zone.seconds, "/value/zone/seconds");
  }
  if (zone.type === "iana") return (offsetAssertion === undefined ? "" : formatOffset(offsetAssertion, "/value/offsetAssertion")) + `[${zone.id}]`;
  if (offsetAssertion !== undefined) fail("unsupported", "no-text-form", "/value/offsetAssertion", "offsetAssertion without a zone has no ISO form");
  return "";
}

/** Formats a civil date, year-month, time or date-time, or a POSIX instant, as ISO 8601 / RFC 9557 text. */
export function formatIso(value: Json): UtvResult {
  return run("formatIso", () => {
    const { kind } = envelope(value, "");
    if (kind === "instant") {
      const { whole, frac } = split(readInstant(value, ""));
      const nano = toNano(frac);
      if (nano === undefined) fail("unsupported", "no-text-form", "/value/subTick", "fractions finer than a nanosecond have no ISO form here");
      const days = whole >= 0n ? whole / 86400n : -((-whole + 86399n) / 86400n);
      const sod = Number(whole - days * 86400n);
      const d = civilFromDays(days);
      const c: Civil = { form: "datetime", ...d, hour: Math.floor(sod / 3600), minute: Math.floor(sod / 60) % 60, second: sod % 60, nano, hasSecond: true, zone: { type: "floating" } };
      return unique(`${formatYear(c.year)}-${pad(c.month)}-${pad(c.day)}T${formatTime(c, true)}Z`);
    }
    if (kind !== "civil") fail("unsupported", "unsupported-operand", "/kind", `formatIso does not handle kind "${kind}"`);
    const c = readCivil(value, "");
    if (c.form !== "datetime" && (c.zone.type !== "floating" || c.offsetAssertion !== undefined)) {
      fail("unsupported", "no-text-form", "/value/zone", `a zoned ${c.form} has no ISO 8601 form`);
    }
    const ymd = `${formatYear(c.year)}-${pad(c.month)}`;
    switch (c.form) {
      case "year-month": return unique(ymd);
      case "date": return unique(`${ymd}-${pad(c.day)}`);
      case "time": return unique(formatTime(c));
      case "datetime": return unique(`${ymd}-${pad(c.day)}T${formatTime(c)}${formatZone(c.zone, c.offsetAssertion)}`);
    }
  });
}
