# UTV text form (profile `utv-text/1`)

The text form is how UTV values look in code, logs, documentation and the examples. It's what `ToString()` / `__str__` / `Display` / `toString()` return, and what `Parse` accepts. JSON stays the interchange format. The text form is a convenience view over it, and it is defined only where it is **lossless**: `parseText(formatText(v)) == v` for every value it can express.

This is an interchange adapter of the kind spec §2 already provides for ("ISO 8601-1/-2 (…), RFC 3339, RFC 9557, … EDTF … have explicit, versioned adapters"). It adds no kinds or properties to the JSON vocabulary, so it doesn't change the specification version or the wire identifier (`utv/1.0`).

Every library implements `parseText` and `formatText`. They are tested by [`conformance/operations/text-form.json`](../conformance/operations/text-form.json).

## Points

These are exactly the forms `parseIso` / `formatIso` already handle (see [`everyday-api.md`](everyday-api.md)):

| Value | Text |
|---|---|
| civil date | `2026-11-05`, `+275760-09-13`, `-0500-03-01` |
| civil year-month | `2026-11` |
| civil time | `09:30`, `09:30:15.25` |
| unzoned civil date-time | `2026-11-05T14:30` |
| fixed-offset date-time | `2026-11-05T14:30+01:00` |
| zoned intention | `2027-06-01T09:00[Europe/Paris]`, `2027-06-01T09:00+02:00[Europe/Paris]` |
| POSIX instant | `2026-11-05T13:30:00Z`, `1970-01-01T00:00:00.5Z` |

## Periods (calendar amounts)

These use ISO 8601 duration syntax, `P[nY][nM][nW][nD][T[nH][nM][nS]]`, with integer components only:

- Components appear in that order. Zero components are omitted.
- The empty period is `P0D`.
- Weeks may be combined with other components. ISO 8601-2 allows this; ISO 8601-1 doesn't.
- A period whose components are **all negative** is written with a leading minus sign: `-P1M`.
- A period with **mixed signs** has no text form. `formatText` returns `unsupported` with diagnostic `no-text-form`.
- `P1M` is a month and `PT1M` is a minute. They are never confused.
- Decimal fractions (`P1.5M`) are `unsupported` (`unsupported-form`), because calendar components are whole numbers.

## Intervals

Intervals are written in bracket notation, with endpoint state explicit. Two points of the same kind or form are separated by `, ` (comma, space):

| Text | Meaning |
|---|---|
| `[2026-11-01, 2026-12-01)` | start included, end excluded |
| `[2026-11-01, 2026-11-30]` | both included |
| `(2026-11-01, 2026-12-01)` | both excluded |
| `[2026-11-01, ..)` | end **unbounded** (there is no end) |
| `[2026-11-01, ?)` | end **unknown** (there is an end, nobody knows it) |
| `(.., 2026-12-01)` | start unbounded |
| `[2026-11-05T13:30:00Z, 2026-11-05T15:30:00Z)` | instants (domain `physical`) |

These rules apply:

- **Unbounded and unknown endpoints.** `..` or `?` must sit on an open bracket: `(` at the start, `)` at the end.
- **Domain.** The JSON `domain` is `civil/gregory` for civil points and `physical` for instants.
- **Parsing:**
  - Whitespace after the comma is optional.
  - Reversed endpoints give `invalid` (`reversed-interval`).
  - Points of different kinds or forms give `invalid` (`kind-mismatch`).
- **Construction.** Only `start-end` construction has a text form. `start-amount` and `amount-end` intervals give `unsupported` from `formatText`.

**Why not `a/b`?** In ISO 8601 and EDTF the slash notation doesn't say which ends are included, and EDTF reads it as closed. UTV keeps endpoint state explicit, so it uses brackets for output. `parseText` still **accepts** the EDTF forms and converts them:

| EDTF input | Parsed as |
|---|---|
| `2026-11-01/2026-11-30` | `[2026-11-01, 2026-11-30]` |
| `2026-11-01/..` | `[2026-11-01, ..)` |
| `../2026-12-01` | `(.., 2026-12-01]` |
| `2026-11-01/` | `[2026-11-01, ?)` |
| `/2026-12-01` | `(?, 2026-12-01]` |

## Not (yet) in the text form

- **Exact second counts** (`posixSeconds`, `elapsed`): no standard notation carries their basis. `formatText` returns `unsupported` (`no-text-form`). Libraries still give them a *display* string such as `3600.75 s (POSIX)` or `86400 s (SI)`, but that string can't be parsed.
  - A decimal is used when the fraction terminates; otherwise the display is a fraction, e.g. `1/3 s (POSIX)`.
- **EDTF qualifiers** (`1347~`, `1347?`, `134X`): these map to UTV's `uncertain` kind, and that mapping needs its own definition. Approximate isn't the same as bounded. Until then, parsing them gives `unsupported` (`unsupported-form`).
- **Everything else** (recurrences, series, other calendars, …) has no text form. It is written as JSON.

## Considered and deferred: a shorter JSON authoring form

The review also suggested letting hand-written JSON omit default values, such as the Gregorian calendar and nested `schema` identifiers. Unlike the text form, that changes which JSON a reader must accept. That makes it a minor specification change (1.1), with a new schema file and reader rules. It is tracked as OI-18 in `spec/OPEN-ISSUES.md` rather than done in libraries ahead of the spec.
